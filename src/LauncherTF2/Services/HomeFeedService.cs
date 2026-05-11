using LauncherTF2.Core;
using LauncherTF2.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LauncherTF2.Services;

/// <summary>
/// Fetches live TF2 content for the Home tab from Steam and GameBanana APIs.
/// Results are cached in-memory for 15 minutes.
/// </summary>
public class HomeFeedService
{
    private const int TF2AppId = 440;
    private const int TF2GameBananaId = 297;
    private const int CacheMinutes = 15;

    /// <summary>
    /// Steam-hosted Team Fortress 2 hero image — used as the news placeholder
    /// when an article doesn't ship its own thumbnail.
    /// </summary>
    private const string NewsPlaceholderUrl =
        "https://cdn.akamai.steamstatic.com/steam/apps/440/header.jpg";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Cheap HTML <img src="..."> matcher — works on every BBCode/HTML mix Valve serves.
    private static readonly Regex _imgSrcRegex = new(
        """<img[^>]+src\s*=\s*["']([^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Markdown-style ![alt](url) image fallback.
    private static readonly Regex _markdownImgRegex = new(
        @"!\[[^\]]*\]\((https?://[^\s)]+\.(?:png|jpe?g|gif|webp))\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private List<NewsItem>? _cachedNews;
    private List<NewModItem>? _cachedMods;
    private DateTime _newsCachedAt = DateTime.MinValue;
    private DateTime _modsCachedAt = DateTime.MinValue;

    public bool LastSteamNewsLoadSucceeded { get; private set; }
    public bool LastGameBananaLoadSucceeded { get; private set; }

    static HomeFeedService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
    }

    public async Task<List<NewsItem>> GetSteamNewsAsync(int count = 5)
    {
        if (_cachedNews != null && (DateTime.UtcNow - _newsCachedAt).TotalMinutes < CacheMinutes)
            return _cachedNews;

        try
        {
            // maxlength=600 gives us enough HTML to find an image; the UI still trims display text.
            var url = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={TF2AppId}&count={count}&maxlength=600&format=json";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var items = new List<NewsItem>();
            var newsItems = doc.RootElement.GetProperty("appnews").GetProperty("newsitems");

            foreach (var item in newsItems.EnumerateArray())
            {
                var contents = item.GetProperty("contents").GetString() ?? "";
                items.Add(new NewsItem
                {
                    Title = item.GetProperty("title").GetString() ?? "",
                    Contents = StripHtml(contents),
                    Date = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("date").GetInt64()).DateTime,
                    Url = item.GetProperty("url").GetString() ?? "",
                    FeedLabel = item.TryGetProperty("feedlabel", out var fl) ? fl.GetString() ?? "" : "",
                    ImageUrl = ExtractFirstImageUrl(contents) ?? NewsPlaceholderUrl
                });
            }

            _cachedNews = items;
            _newsCachedAt = DateTime.UtcNow;
            LastSteamNewsLoadSucceeded = true;
            Logger.LogInfo($"[HomeFeed] Loaded {items.Count} Steam news items");
            return items;
        }
        catch (Exception ex)
        {
            LastSteamNewsLoadSucceeded = false;
            Logger.LogWarning($"[HomeFeed] Steam news fetch failed: {ex.Message}");
            return [];
        }
    }

    public async Task<List<NewModItem>> GetNewModsAsync(int count = 8)
    {
        if (_cachedMods != null && (DateTime.UtcNow - _modsCachedAt).TotalMinutes < CacheMinutes)
            return _cachedMods;

        try
        {
            // Pull more than asked-for so NSFW filtering still leaves a full row.
            var fetchCount = Math.Min(count * 3, 50);
            var url = $"https://gamebanana.com/apiv11/Mod/Index?_nPage=1&_nPerpage={fetchCount}" +
                      $"&_csvProperties=_idRow,_sName,_aPreviewMedia,_aSubmitter,_tsDateAdded,_bIsNsfw,_sProfileUrl" +
                      $"&_aFilters[Generic_Game]={TF2GameBananaId}&_sSort=Generic_LatestModified";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var items = new List<NewModItem>();
            var records = doc.RootElement.GetProperty("_aRecords");

            foreach (var rec in records.EnumerateArray())
            {
                if (items.Count >= count)
                    break;

                // Skip anything flagged as NSFW by GameBanana.
                if (rec.TryGetProperty("_bIsNsfw", out var nsfw) &&
                    nsfw.ValueKind == JsonValueKind.True)
                    continue;

                string? thumbUrl = null;
                if (rec.TryGetProperty("_aPreviewMedia", out var media) &&
                    media.TryGetProperty("_aImages", out var images) &&
                    images.GetArrayLength() > 0)
                {
                    var img = images[0];
                    if (img.TryGetProperty("_sBaseUrl", out var baseUrl))
                    {
                        var file = TryGetStr(img, "_sFile530") ?? TryGetStr(img, "_sFile220") ?? TryGetStr(img, "_sFile");
                        if (file != null)
                            thumbUrl = $"{baseUrl.GetString()}/{file}";
                    }
                }

                var author = "Unknown";
                if (rec.TryGetProperty("_aSubmitter", out var sub) &&
                    sub.TryGetProperty("_sName", out var sName))
                    author = sName.GetString() ?? "Unknown";

                items.Add(new NewModItem
                {
                    Name = rec.GetProperty("_sName").GetString() ?? "",
                    Author = author,
                    ThumbnailUrl = thumbUrl,
                    ProfileUrl = rec.TryGetProperty("_sProfileUrl", out var pUrl) ? pUrl.GetString() ?? "" : "",
                    DateAdded = rec.TryGetProperty("_tsDateAdded", out var ts)
                        ? DateTimeOffset.FromUnixTimeSeconds(ts.GetInt64()).DateTime
                        : DateTime.UtcNow
                });
            }

            _cachedMods = items;
            _modsCachedAt = DateTime.UtcNow;
            LastGameBananaLoadSucceeded = true;
            Logger.LogInfo($"[HomeFeed] Loaded {items.Count} GameBanana mods (NSFW filtered)");
            return items;
        }
        catch (Exception ex)
        {
            LastGameBananaLoadSucceeded = false;
            Logger.LogWarning($"[HomeFeed] GameBanana fetch failed: {ex.Message}");
            return [];
        }
    }

    /// <summary>Invalidates caches so next call fetches fresh data.</summary>
    public void InvalidateCache()
    {
        _newsCachedAt = DateTime.MinValue;
        _modsCachedAt = DateTime.MinValue;
    }

    /// <summary>Returns the first image URL referenced by the article's HTML/markdown body.</summary>
    private static string? ExtractFirstImageUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var img = _imgSrcRegex.Match(html);
        if (img.Success) return img.Groups[1].Value;

        var md = _markdownImgRegex.Match(html);
        if (md.Success) return md.Groups[1].Value;

        return null;
    }

    /// <summary>Lightweight HTML stripper so card body text reads cleanly.</summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        // Drop tags, collapse whitespace, decode the few HTML entities we actually see.
        var noTags = Regex.Replace(html, "<[^>]+>", " ");
        var collapsed = Regex.Replace(noTags, @"\s+", " ").Trim();
        return collapsed
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">");
    }

    private static string? TryGetStr(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v))
        {
            var s = v.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }
}
