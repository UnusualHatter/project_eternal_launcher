using LauncherTF2.Core;
using LauncherTF2.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LauncherTF2.Services;


/// <summary>
/// Enriches local mod metadata using a two-step lookup:
///
///   1. DuckDuckGo HTML search  → "{modName} site:gamebanana.com"
///      Extracts GameBanana mod URLs from the search results (no API key needed).
///
///   2. GameBanana API per-item → validates game == TF2 (ID 297) and that the
///      returned title is sufficiently similar to the local mod name.
///
/// If a validated match is found, the thumbnail is downloaded to a local cache
/// and the mod's Author / ThumbnailPath are updated in-place.
///
/// Cache: 7-day expiry. Negative entries cached for 7 days too (no re-fetching).
/// </summary>
public class GameBananaEnrichmentService
{
    private const int TF2GameId = 297;
    private const int CacheDays = 7;

    // DuckDuckGo HTML search (no API key required)
    private const string DDGSearchUrl =
        "https://html.duckduckgo.com/html/?q={0}+%22Team+Fortress+2%22+site%3Agamebanana.com";

    // GameBanana API per-item endpoint
    private const string GBItemApiUrl =
        "https://gamebanana.com/apiv11/{0}/{1}?_csvProperties=_sName,_aGame,_aPreviewMedia,_aSubmitter";

    // Regex: extracts section + numeric ID from GameBanana URLs inside HTML
    private static readonly Regex GBUrlRegex = new(
        @"gamebanana\.com/(mods|sounds|guis|maps|skins|effects|sprays)/(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { }
    };

    static GameBananaEnrichmentService()
    {
        // Browser-like User-Agent so search engines don't reject the request
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/124.0.0.0 Safari/537.36");

        _http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    private readonly string _thumbnailCacheDir;
    private readonly string _metadataCachePath;
    private readonly object _saveLock = new();
    private ConcurrentDictionary<string, CachedEntry> _cache;
    private Timer? _debounceSaveTimer;
    private const int SaveDebounceMs = 5000;

    public GameBananaEnrichmentService()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _thumbnailCacheDir = Path.Combine(baseDir, "mod_thumbnails");
        _metadataCachePath = Path.Combine(baseDir, "mod_metadata_cache.json");

        Directory.CreateDirectory(_thumbnailCacheDir);
        _cache = LoadCache();
    }

    // ──────────────────────── Public API ───────────────────────────────────

    public async Task EnrichModAsync(ModModel mod)
    {
        var key = NormalizeKey(mod.Name);

        // Cache hit (positive or negative)
        if (_cache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            if (!cached.IsNegative) ApplyEntry(mod, cached);
            return;
        }

        try
        {
            // Step 1: DuckDuckGo search → collect candidate GameBanana IDs
            var candidates = await SearchDuckDuckGoAsync(mod.Name);

            if (candidates.Count == 0)
            {
                Logger.LogInfo($"[Enrichment] No DDG results for '{mod.Name}'");
                StoreNegative(key);
                return;
            }

            // Step 2: For each candidate, call GameBanana API and validate
            foreach (var (section, id) in candidates)
            {
                var result = await ValidateWithGameBananaAsync(section, id, mod.Name);
                if (result == null) continue;

                // Match found!
                var thumbPath = await DownloadThumbnailAsync(key, result.ThumbnailUrl);

                var entry = new CachedEntry
                {
                    Author = result.Author,
                    ThumbnailLocalPath = thumbPath,
                    FetchedAt = DateTime.UtcNow,
                    IsNegative = false
                };

                _cache[key] = entry;
                ScheduleCacheSave();
                ApplyEntry(mod, entry);

                Logger.LogInfo($"[Enrichment] Matched '{mod.Name}' → '{result.GbName}' by {result.Author}");
                return;
            }

            Logger.LogInfo($"[Enrichment] No TF2 match for '{mod.Name}' ({candidates.Count} candidates checked)");
            StoreNegative(key);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Enrichment] Failed for '{mod.Name}': {ex.Message}");
        }
    }

    // ──────────────────────── Step 1: DuckDuckGo search ────────────────────

    private async Task<List<(string Section, string Id)>> SearchDuckDuckGoAsync(string modName)
    {
        var query = Uri.EscapeDataString(modName);
        var url = string.Format(DDGSearchUrl, query);

        var html = await _http.GetStringAsync(url);

        // Extract unique GameBanana URLs from the HTML
        var seen = new HashSet<string>();
        var results = new List<(string, string)>();

        foreach (Match m in GBUrlRegex.Matches(html))
        {
            var section = m.Groups[1].Value.ToLowerInvariant();
            var id = m.Groups[2].Value;
            var dedup = $"{section}/{id}";

            if (seen.Add(dedup))
                results.Add((section, id));

            if (results.Count >= 8) break; // No need to check too many
        }

        return results;
    }

    // ──────────────────────── Step 2: GameBanana API validation ────────────

    private record GbMatch(string GbName, string Author, string? ThumbnailUrl);

    private async Task<GbMatch?> ValidateWithGameBananaAsync(
        string section, string id, string localName)
    {
        try
        {
            var modelName = SectionToModelName(section);
            var url = string.Format(GBItemApiUrl, modelName, id);
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ── Must be TF2 ──────────────────────────────────────────────
            if (!root.TryGetProperty("_aGame", out var game)) return null;
            if (!game.TryGetProperty("_idRow", out var gameId)) return null;
            if (gameId.GetInt32() != TF2GameId) return null;

            // ── Name similarity check ────────────────────────────────────
            var gbName = root.TryGetProperty("_sName", out var n) ? n.GetString() ?? "" : "";
            if (!AreNamesSimilar(localName, gbName))
            {
                Logger.LogInfo($"  [Enrichment] Skipping '{gbName}' — name mismatch for '{localName}'");
                return null;
            }

            // ── Author ───────────────────────────────────────────────────
            var author = "Unknown";
            if (root.TryGetProperty("_aSubmitter", out var sub) &&
                sub.TryGetProperty("_sName", out var sName))
                author = sName.GetString() ?? "Unknown";

            // ── Thumbnail URL ─────────────────────────────────────────────
            string? thumbUrl = null;
            if (root.TryGetProperty("_aPreviewMedia", out var media) &&
                media.TryGetProperty("_aImages", out var images) &&
                images.GetArrayLength() > 0)
            {
                var img = images[0];
                if (img.TryGetProperty("_sBaseUrl", out var baseUrl))
                {
                    var fileField =
                        TryGetStr(img, "_sFile530") ??
                        TryGetStr(img, "_sFile220") ??
                        TryGetStr(img, "_sFile100") ??
                        TryGetStr(img, "_sFile");

                    if (fileField != null)
                        thumbUrl = $"{baseUrl.GetString()}/{fileField}";
                }
            }

            return new GbMatch(gbName, author, thumbUrl);
        }
        catch
        {
            return null; // API request failed for this ID — skip it
        }
    }

    // ──────────────────────── Name similarity ──────────────────────────────

    /// <summary>
    /// Two names are "similar" if one contains the other (after normalisation)
    /// or their edit-distance similarity is above 60 %.
    /// </summary>
    private static bool AreNamesSimilar(string local, string gb)
    {
        var a = NormalizeForCompare(local);
        var b = NormalizeForCompare(gb);

        if (a.Length < 3 || b.Length < 3) return false;

        // Substring match (e.g. "amphuddx" ⊂ "amphuddx hud")
        if (a.Contains(b, StringComparison.OrdinalIgnoreCase)) return true;
        if (b.Contains(a, StringComparison.OrdinalIgnoreCase)) return true;

        // Common word match (e.g. "ahud_v2" vs "TF2 ahud")
        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var wa in wordsA)
        {
            if (wa.Length >= 4 && wordsB.Any(wb => wb == wa)) return true;
        }

        // Levenshtein similarity > 40 %
        var sim = LevenshteinSimilarity(a, b);
        return sim >= 0.40;
    }

    private static string NormalizeForCompare(string s)
    {
        // Remove special chars, collapse whitespace, lowercase
        s = Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9\s]", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static double LevenshteinSimilarity(string a, string b)
    {
        if (a == b) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = a[i - 1] == b[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));

        int maxLen = Math.Max(a.Length, b.Length);
        return 1.0 - (double)d[a.Length, b.Length] / maxLen;
    }

    // ──────────────────────── Helpers ──────────────────────────────────────

    private static string SectionToModelName(string section) => section switch
    {
        "sounds" => "Sound",
        "guis"   => "Gui",
        "maps"   => "Map",
        "skins"  => "Skin",
        "effects"=> "Effect",
        "sprays" => "Spray",
        _        => "Mod"
    };

    private static string? TryGetStr(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v))
        {
            var s = v.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }

    private static void ApplyEntry(ModModel mod, CachedEntry entry)
    {
        if (entry.IsNegative) return;

        if (!string.IsNullOrWhiteSpace(entry.Author) && entry.Author != "Unknown")
            mod.Author = entry.Author;

        if (!string.IsNullOrWhiteSpace(entry.ThumbnailLocalPath) &&
            File.Exists(entry.ThumbnailLocalPath))
        {
            mod.ThumbnailPath = entry.ThumbnailLocalPath;
            mod.IsEnriched = true;
        }
    }

    private async Task<string?> DownloadThumbnailAsync(string key, string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return null;
        try
        {
            var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var localPath = Path.Combine(_thumbnailCacheDir, $"{key}{ext}");
            var bytes = await _http.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(localPath, bytes);
            return localPath;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Enrichment] Thumbnail download failed ({imageUrl}): {ex.Message}");
            return null;
        }
    }

    private void StoreNegative(string key)
    {
        _cache[key] = CachedEntry.Negative();
        ScheduleCacheSave();
    }

    /// <summary>
    /// Debounces cache saves so we don't write the full JSON file after every single mod.
    /// Flushes 5 seconds after the last change.
    /// </summary>
    private void ScheduleCacheSave()
    {
        _debounceSaveTimer?.Dispose();
        _debounceSaveTimer = new Timer(_ => SaveCache(), null, SaveDebounceMs, Timeout.Infinite);
    }

    private static string NormalizeKey(string name)
    {
        var key = name.ToLowerInvariant().Trim();
        key = Regex.Replace(key, @"[^\w\s-]", "");
        key = Regex.Replace(key, @"\s+", "_");
        return key;
    }

    // ──────────────────────── Cache persistence ─────────────────────────────

    private ConcurrentDictionary<string, CachedEntry> LoadCache()
    {
        try
        {
            if (File.Exists(_metadataCachePath))
            {
                var json = File.ReadAllText(_metadataCachePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, CachedEntry>>(json);
                if (dict != null)
                    return new ConcurrentDictionary<string, CachedEntry>(dict);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Enrichment] Failed to load cache: {ex.Message}");
        }
        return new ConcurrentDictionary<string, CachedEntry>();
    }

    private void SaveCache()
    {
        try
        {
            // Lock so concurrent tasks don't write simultaneously
            lock (_saveLock)
            {
                var json = JsonSerializer.Serialize(
                    new Dictionary<string, CachedEntry>(_cache),
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataCachePath, json);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Enrichment] Failed to save cache: {ex.Message}");
        }
    }

    // ──────────────────────── Cache model ──────────────────────────────────

    private class CachedEntry
    {
        public string? Author { get; set; }
        public string? ThumbnailLocalPath { get; set; }
        public DateTime FetchedAt { get; set; }
        public bool IsNegative { get; set; }

        public bool IsExpired => (DateTime.UtcNow - FetchedAt).TotalDays > CacheDays;

        public static CachedEntry Negative() => new()
        {
            IsNegative = true,
            FetchedAt = DateTime.UtcNow
        };
    }
}
