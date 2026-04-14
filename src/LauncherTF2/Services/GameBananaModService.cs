using LauncherTF2.Core;
using LauncherTF2.Models;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LauncherTF2.Services;

public class GameBananaModService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly Regex ModIdRegex = new(@"mods/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex XmlEnvelopeRegex = new(@"<\?xml[\s\S]*?</rss>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const string Tf2GameId = "297";
    private const string Tf2GameName = "Team Fortress 2";
    private static readonly string GameSectionsUrl = $"https://gamebanana.com/apiv11/Game/{Tf2GameId}/Sections";
    private static readonly string GameSubfeedUrl = $"https://gamebanana.com/apiv11/Game/{Tf2GameId}/Subfeed";
    private readonly string _catalogCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "online_mod_catalog_cache.json");

    static GameBananaModService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ProjectEternalLauncher/1.0");
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<ModModel>> GetCatalogAsync(int? sectionId = null, string sort = "new", string? searchQuery = null, int page = 1)
    {
        try
        {
            var pageResult = await GetCatalogPageAsync(sectionId, sort, searchQuery, page);
            if (pageResult.Mods.Count > 0)
            {
                SaveCache(pageResult.Mods);
            }

            Logger.LogInfo($"Loaded {pageResult.Mods.Count} online mods from GameBanana (page {pageResult.Page})");
            return pageResult.Mods;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load online mod catalog", ex);
            return LoadCache();
        }
    }

    public async Task<GameBananaCatalogPage> GetCatalogPageAsync(int? sectionId = null, string sort = "new", string? searchQuery = null, int page = 1)
    {
        try
        {
            return await LoadSubfeedAsync(sectionId, sort, searchQuery, page);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load online mod catalog page", ex);
            return new GameBananaCatalogPage(new List<ModModel>(), page, 0, true, 0);
        }
    }

    public async Task<List<GameSection>> GetSectionsAsync()
    {
        try
        {
            using var response = await HttpClient.GetAsync(GameSectionsUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var sections = JsonSerializer.Deserialize<List<GameSection>>(json) ?? new List<GameSection>();

            Logger.LogInfo($"Loaded {sections.Count} sections from GameBanana");
            return sections;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load sections from GameBanana", ex);
            return new List<GameSection>();
        }
    }

    public async Task<bool> DownloadAndInstallModAsync(ModModel mod, ModManagerService modManagerService)
    {
        string? tempPath = null;
        string? extractedDirectory = null;

        try
        {
            if (mod.SourceKind != ModSourceKind.Online || string.IsNullOrWhiteSpace(mod.SourceUrl))
            {
                Logger.LogWarning("Cannot download non-online mod or mod without source URL");
                return false;
            }

            var modId = ExtractModId(mod.SourceUrl);
            if (modId <= 0)
            {
                Logger.LogWarning($"Invalid online mod URL: {mod.SourceUrl}");
                return false;
            }

            var downloadInfo = await GetFirstDownloadFileAsync(modId);
            if (downloadInfo == null || string.IsNullOrWhiteSpace(downloadInfo.DownloadUrl))
            {
                Logger.LogWarning($"No downloadable file found for mod {modId}");
                return false;
            }

            if (!string.Equals(downloadInfo.GameName, Tf2GameName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning($"Skipping non-TF2 mod download: {downloadInfo.GameName}");
                return false;
            }

            var safeFileName = GetSafeFileName(downloadInfo.FileName);
            tempPath = Path.Combine(Path.GetTempPath(), safeFileName);

            using (var response = await HttpClient.GetAsync(downloadInfo.DownloadUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(tempPath);
                await stream.CopyToAsync(output);
            }

            bool installed;
            var extension = Path.GetExtension(tempPath).ToLowerInvariant();

            if (extension == ".zip")
            {
                extractedDirectory = Path.Combine(Path.GetTempPath(), $"tf2_mod_{modId}_{Guid.NewGuid():N}");
                Directory.CreateDirectory(extractedDirectory);
                ZipFile.ExtractToDirectory(tempPath, extractedDirectory, true);
                installed = modManagerService.InstallMod(extractedDirectory);
            }
            else
            {
                installed = modManagerService.InstallMod(tempPath);
            }

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (!string.IsNullOrWhiteSpace(extractedDirectory) && Directory.Exists(extractedDirectory))
            {
                Directory.Delete(extractedDirectory, true);
            }

            if (installed)
            {
                Logger.LogInfo($"Downloaded and installed mod: {downloadInfo.ModName}");
            }

            return installed;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to download and install mod: {mod.Name}", ex);
            return false;
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                if (!string.IsNullOrWhiteSpace(extractedDirectory) && Directory.Exists(extractedDirectory))
                {
                    Directory.Delete(extractedDirectory, true);
                }
            }
            catch
            {
            }
        }
    }

    private async Task<GameBananaCatalogPage> LoadSubfeedAsync(int? sectionId, string sort, string? searchQuery, int page)
    {
        var baseUrl = sectionId.HasValue
            ? $"https://gamebanana.com/apiv11/Section/{sectionId.Value}/Subfeed"
            : GameSubfeedUrl;

        var query = $"?_nPage={page}&_sSort={Uri.EscapeDataString(sort)}";
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query += $"&_sSearch={Uri.EscapeDataString(searchQuery.Trim())}";
        }

        var url = baseUrl + query;

        using var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var seeds = new List<ModModel>();
        int perPage = 0;
        int totalRecords = 0;
        bool isComplete = false;

        if (document.RootElement.TryGetProperty("_aMetadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            if (metadata.TryGetProperty("_nPerpage", out var perPageElement) && perPageElement.ValueKind == JsonValueKind.Number)
            {
                perPage = perPageElement.GetInt32();
            }
            if (metadata.TryGetProperty("_nRecordCount", out var totalElement) && totalElement.ValueKind == JsonValueKind.Number)
            {
                totalRecords = totalElement.GetInt32();
            }
            if (metadata.TryGetProperty("_bIsComplete", out var completeElement) && completeElement.ValueKind == JsonValueKind.True)
            {
                isComplete = true;
            }
        }

        if (document.RootElement.TryGetProperty("_aRecords", out var recordsElement) &&
            recordsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var record in recordsElement.EnumerateArray())
            {
                var seed = ParseModFromJson(record);
                if (seed != null)
                {
                    seeds.Add(CreateModFromSeed(seed));
                }
            }
        }

        return new GameBananaCatalogPage(seeds, page, perPage, isComplete, totalRecords);
    }

    private RemoteModSeed? ParseModFromJson(JsonElement record)
    {
        try
        {
            if (!record.TryGetProperty("_sModelName", out var modelNameElement) ||
                modelNameElement.GetString() != "Mod")
            {
                return null;
            }

            if (record.TryGetProperty("_bHasFiles", out var hasFilesElement) &&
                hasFilesElement.ValueKind == JsonValueKind.False)
            {
                return null;
            }

            var id = record.GetProperty("_idRow").GetInt32();
            var name = record.GetProperty("_sName").GetString() ?? string.Empty;

            string author = "Unknown";
            if (record.TryGetProperty("_aSubmitter", out var submitterElement) &&
                submitterElement.ValueKind == JsonValueKind.Object &&
                submitterElement.TryGetProperty("_sName", out var nameElement))
            {
                author = nameElement.GetString() ?? "Unknown";
            }

            var categories = new List<string> { "Online" };
            if (record.TryGetProperty("_aRootCategory", out var rootCategory) &&
                rootCategory.ValueKind == JsonValueKind.Object &&
                rootCategory.TryGetProperty("_sName", out var rootName) &&
                !string.IsNullOrWhiteSpace(rootName.GetString()))
            {
                categories.Add(rootName.GetString()!.Trim());
            }

            if (record.TryGetProperty("_aSubCategory", out var subCategory) &&
                subCategory.ValueKind == JsonValueKind.Object &&
                subCategory.TryGetProperty("_sName", out var subName) &&
                !string.IsNullOrWhiteSpace(subName.GetString()))
            {
                categories.Add(subName.GetString()!.Trim());
            }

            var description = GetDescription(record, categories);
            var imageUrl = GetThumbnailUrl(record);

            return new RemoteModSeed
            {
                Id = id,
                Title = name,
                Author = author,
                Description = description,
                Categories = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                ImageUrl = imageUrl,
                SourceLabel = "GameBanana"
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to parse mod from JSON: {ex.Message}");
            return null;
        }
    }

    private static string GetThumbnailUrl(JsonElement record)
    {
        if (record.TryGetProperty("_aPreviewMedia", out var previewMedia))
        {
            if (previewMedia.ValueKind == JsonValueKind.Object &&
                previewMedia.TryGetProperty("_aImages", out var imageArray) &&
                imageArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var image in imageArray.EnumerateArray())
                {
                    if (TryBuildThumbnailUrl(image, out var url))
                    {
                        return NormalizeThumbnailUrl(url);
                    }
                }
            }
            else if (previewMedia.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in previewMedia.EnumerateArray())
                {
                    if (TryBuildThumbnailUrl(item, out var url))
                    {
                        return NormalizeThumbnailUrl(url);
                    }
                }
            }
        }

        return "/Resources/Assets/logo.png";
    }

    private static bool TryBuildThumbnailUrl(JsonElement image, out string url)
    {
        url = string.Empty;
        if (image.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (TryGetStringProperty(image, "_sBaseUrl", out var baseUrl))
        {
            if (TryGetStringProperty(image, "_sFile220", out var fileName) ||
                TryGetStringProperty(image, "_sFile530", out fileName) ||
                TryGetStringProperty(image, "_sFile100", out fileName) ||
                TryGetStringProperty(image, "_sFile", out fileName))
            {
                url = $"{baseUrl.TrimEnd('/')}/{fileName.TrimStart('/')}";
                return true;
            }
        }

        if (TryGetStringProperty(image, "_sFullUrl", out url) || TryGetStringProperty(image, "_sUrl", out url))
        {
            return true;
        }

        return false;
    }

    private static string GetDescription(JsonElement record, IEnumerable<string> categoryFallback)
    {
        if (record.TryGetProperty("_aPreviewMedia", out var previewMedia) &&
            previewMedia.ValueKind == JsonValueKind.Object &&
            previewMedia.TryGetProperty("_aMetadata", out var metadata) &&
            metadata.ValueKind == JsonValueKind.Object &&
            metadata.TryGetProperty("_sSnippet", out var snippet) &&
            snippet.ValueKind == JsonValueKind.String)
        {
            var value = snippet.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Join(" • ", categoryFallback.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static string NormalizeThumbnailUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/Resources/Assets/logo.png";
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + url;
        }

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return "https://gamebanana.com" + url;
        }

        return url;
    }

    private async Task<List<RemoteModSeed>> LoadFeedAsync(string feedUrl, string sourceLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        using var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rawContent = await response.Content.ReadAsStringAsync();
        var xml = ExtractXmlEnvelope(rawContent);
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

        return document
            .Descendants("item")
            .Select(item => new RemoteModSeed
            {
                Id = ExtractModId(item.Element("link")?.Value),
                Title = item.Element("title")?.Value?.Trim() ?? string.Empty,
                ImageUrl = item.Element("image")?.Value?.Trim() ?? string.Empty,
                SourceLabel = sourceLabel
            })
            .Where(item => item.Id > 0)
            .ToList();
    }

    private static string ExtractXmlEnvelope(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException("Empty RSS response");
        }

        var match = XmlEnvelopeRegex.Match(content);
        if (match.Success)
        {
            return match.Value;
        }

        var start = content.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
        var end = content.IndexOf("</rss>", StringComparison.OrdinalIgnoreCase);

        if (start >= 0 && end > start)
        {
            return content.Substring(start, (end - start) + "</rss>".Length);
        }

        throw new InvalidDataException("Invalid RSS envelope in GameBanana response");
    }

    private async Task<DownloadFileInfo?> GetFirstDownloadFileAsync(int modId)
    {
        var fields = "name,Game().name,Files().aFiles()";
        var endpoint = $"https://api.gamebanana.com/Core/Item/Data?itemtype=Mod&itemid={modId}&fields={Uri.EscapeDataString(fields)}";

        using var response = await HttpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync();
        var trimmedJson = rawJson.TrimStart();
        var json = trimmedJson.StartsWith("[") || trimmedJson.StartsWith("{")
            ? rawJson
            : ExtractJsonEnvelope(rawJson);
        using var document = JsonDocument.Parse(json);

        JsonElement payload;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            payload = document.RootElement;
        }
        else if (!document.RootElement.TryGetProperty("value", out payload) || payload.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        if (payload.GetArrayLength() < 3)
        {
            return null;
        }

        var modName = payload[0].GetString() ?? $"Mod {modId}";
        var gameName = payload[1].GetString() ?? string.Empty;
        var filesObject = payload[2];

        if (filesObject.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var entry in filesObject.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fileNode = entry.Value;

            if (!fileNode.TryGetProperty("_sDownloadUrl", out var downloadUrlNode))
            {
                continue;
            }

            var downloadUrl = downloadUrlNode.GetString();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var fileName = fileNode.TryGetProperty("_sFile", out var fileNodeName)
                ? (fileNodeName.GetString() ?? $"mod_{modId}.dat")
                : $"mod_{modId}.dat";

            return new DownloadFileInfo
            {
                ModName = modName,
                GameName = gameName,
                FileName = fileName,
                DownloadUrl = downloadUrl
            };
        }

        return null;
    }

    private static string ExtractJsonEnvelope(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException("Empty JSON response");
        }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            return content.Substring(start, end - start + 1);
        }

        throw new InvalidDataException("Invalid JSON envelope in GameBanana response");
    }

    private static string GetSafeFileName(string fileName)
    {
        var value = string.IsNullOrWhiteSpace(fileName) ? "downloaded_mod.dat" : fileName;

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private void SaveCache(List<ModModel> mods)
    {
        try
        {
            var cache = mods.Select(mod => new CachedMod
            {
                Id = ExtractModId(mod.SourceUrl),
                Name = mod.Name,
                ThumbnailPath = mod.ThumbnailPath,
                SourceLabel = mod.SourceLabel,
                SourceUrl = mod.SourceUrl,
                GameName = Tf2GameName
            }).ToList();

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_catalogCachePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to save online catalog cache", ex);
        }
    }

    private List<ModModel> LoadCache()
    {
        try
        {
            if (!File.Exists(_catalogCachePath))
            {
                return new List<ModModel>();
            }

            var json = File.ReadAllText(_catalogCachePath);
            var cache = JsonSerializer.Deserialize<List<CachedMod>>(json) ?? new List<CachedMod>();

            var mods = cache
                .Where(item => item.Id > 0 && string.Equals(item.GameName, Tf2GameName, StringComparison.OrdinalIgnoreCase))
                .Select(item => CreateModFromSeed(new RemoteModSeed
                {
                    Id = item.Id,
                    Title = item.Name,
                    ImageUrl = item.ThumbnailPath,
                    SourceLabel = string.IsNullOrWhiteSpace(item.SourceLabel) ? "Cache" : item.SourceLabel
                }))
                .ToList();

            if (mods.Count > 0)
            {
                Logger.LogInfo($"Loaded {mods.Count} online mods from local cache");
            }

            return mods;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load online catalog cache", ex);
            return new List<ModModel>();
        }
    }

    private static ModModel CreateModFromSeed(RemoteModSeed seed)
    {
        return new ModModel
        {
            Name = string.IsNullOrWhiteSpace(seed.Title) ? $"Mod {seed.Id}" : seed.Title,
            Author = string.IsNullOrWhiteSpace(seed.Author) ? "GameBanana" : seed.Author,
            Description = string.IsNullOrWhiteSpace(seed.Description)
                ? "Online mod available from GameBanana catalog."
                : seed.Description,
            Version = "Online",
            ModPath = string.Empty,
            LastModified = DateTime.UtcNow,
            ModType = ModType.Custom,
            ThumbnailPath = string.IsNullOrWhiteSpace(seed.ImageUrl) ? "/Resources/Assets/logo.png" : seed.ImageUrl,
            SourceKind = ModSourceKind.Online,
            SourceLabel = seed.SourceLabel,
            SourceUrl = $"https://gamebanana.com/mods/{seed.Id}",
            Categories = new ObservableCollection<string>(seed.Categories.Any()
                ? seed.Categories.Distinct(StringComparer.OrdinalIgnoreCase)
                : new[] { "Online" })
        };
    }

    private static int ExtractModId(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return 0;
        }

        var match = ModIdRegex.Match(link);
        return match.Success && int.TryParse(match.Groups[1].Value, out var modId)
            ? modId
            : 0;
    }

    private sealed class RemoteModSeed
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new();
        public string ImageUrl { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = string.Empty;
    }

    private sealed class CachedMod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public string GameName { get; set; } = string.Empty;
    }

    private sealed class DownloadFileInfo
    {
        public string ModName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public sealed class GameBananaCatalogPage
    {
        public GameBananaCatalogPage(List<ModModel> mods, int page, int perPage, bool isComplete, int totalRecords)
        {
            Mods = mods ?? new List<ModModel>();
            Page = page;
            PerPage = perPage;
            IsComplete = isComplete;
            TotalRecords = totalRecords;
        }

        public List<ModModel> Mods { get; }
        public int Page { get; }
        public int PerPage { get; }
        public bool IsComplete { get; }
        public int TotalRecords { get; }

        // HasMore is true if the API indicates there are more records in the catalog (regardless of filtering)
        public bool HasMore => !IsComplete || (TotalRecords > 0 && TotalRecords > Page * PerPage);
    }
}