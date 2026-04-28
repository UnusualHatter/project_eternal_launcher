using LauncherTF2.Core;
using LauncherTF2.Models;
using System.Net.Http;
using System.Text.Json;

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

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private List<NewsItem>? _cachedNews;
    private List<NewModItem>? _cachedMods;
    private DateTime _newsCachedAt = DateTime.MinValue;
    private DateTime _modsCachedAt = DateTime.MinValue;

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
            var url = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={TF2AppId}&count={count}&maxlength=300&format=json";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var items = new List<NewsItem>();
            var newsItems = doc.RootElement.GetProperty("appnews").GetProperty("newsitems");

            foreach (var item in newsItems.EnumerateArray())
            {
                items.Add(new NewsItem
                {
                    Title = item.GetProperty("title").GetString() ?? "",
                    Contents = item.GetProperty("contents").GetString() ?? "",
                    Date = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("date").GetInt64()).DateTime,
                    Url = item.GetProperty("url").GetString() ?? "",
                    FeedLabel = item.TryGetProperty("feedlabel", out var fl) ? fl.GetString() ?? "" : ""
                });
            }

            _cachedNews = items;
            _newsCachedAt = DateTime.UtcNow;
            Logger.LogInfo($"[HomeFeed] Loaded {items.Count} Steam news items");
            return items;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[HomeFeed] Steam news fetch failed: {ex.Message}");
            return _cachedNews ?? [];
        }
    }

    public async Task<List<NewModItem>> GetNewModsAsync(int count = 8)
    {
        if (_cachedMods != null && (DateTime.UtcNow - _modsCachedAt).TotalMinutes < CacheMinutes)
            return _cachedMods;

        try
        {
            var url = $"https://gamebanana.com/apiv11/Mod/Index?_nPage=1&_nPerpage={count}" +
                      $"&_csvProperties=_idRow,_sName,_aPreviewMedia,_aSubmitter,_tsDateAdded" +
                      $"&_aFilters[Generic_Game]={TF2GameBananaId}&_sSort=Generic_LatestModified";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var items = new List<NewModItem>();
            var records = doc.RootElement.GetProperty("_aRecords");

            foreach (var rec in records.EnumerateArray())
            {
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
            Logger.LogInfo($"[HomeFeed] Loaded {items.Count} GameBanana mods");
            return items;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[HomeFeed] GameBanana fetch failed: {ex.Message}");
            return _cachedMods ?? [];
        }
    }

    /// <summary>Invalidates caches so next call fetches fresh data.</summary>
    public void InvalidateCache()
    {
        _newsCachedAt = DateTime.MinValue;
        _modsCachedAt = DateTime.MinValue;
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
