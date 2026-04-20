using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

public class InventoryPricingService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Ordered for UI display. Backend currently provides live/approx data for prices.tf and Steam Market.
    public static readonly string[] StoreOrder =
    [
        "prices.tf",
        "Steam Market",
        "backpack.tf",
        "marketplace.tf",
        "mannco.store",
        "stntrading.eu",
        "tradeit.gg"
    ];

    private static readonly string AggregatorBaseUrl =
        Environment.GetEnvironmentVariable("TF2_PRICING_AGGREGATOR_URL")?.Trim()
        ?? "http://localhost:5204/api/prices";

    public async Task<PriceSnapshot> GetPriceSnapshotAsync(string itemName, string quality, bool tradable, ApiKeys keys)
    {
        var response = await FetchFromAggregatorAsync(itemName, null);
        var mapped = response == null
            ? BuildUnavailableSnapshot(itemName).StoreResults
            : MapStoreResults(response.Prices, itemName);

        if (AreAllStoresUnavailable(mapped) && TryGetLocalApproxUsd(itemName, out var localUsd))
        {
            mapped["prices.tf"] = new PriceResult
            {
                StoreName = "prices.tf",
                Status = "Approx (local fallback)",
                PriceRef = $"${localUsd:0.00}",
                ListingUrl = GetStoreSearchUrl("prices.tf", itemName)
            };
        }

        var pure = mapped.Values.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.PriceRef));

        return new PriceSnapshot
        {
            StoreResults = mapped,
            PureSummary = pure == null ? "unavailable" : pure.PriceRef
        };
    }

    public async Task<PriceResult> GetSingleStorePriceAsync(string storeName, string itemName, string quality, bool tradable, ApiKeys keys)
    {
        var response = await FetchFromAggregatorAsync(itemName, null);
        var mapped = response == null
            ? BuildUnavailableSnapshot(itemName).StoreResults
            : MapStoreResults(response.Prices, itemName);

        if (AreAllStoresUnavailable(mapped) &&
            TryGetLocalApproxUsd(itemName, out var localUsd) &&
            string.Equals(storeName, "prices.tf", StringComparison.OrdinalIgnoreCase))
        {
            return new PriceResult
            {
                StoreName = "prices.tf",
                Status = "Approx (local fallback)",
                PriceRef = $"${localUsd:0.00}",
                ListingUrl = GetStoreSearchUrl("prices.tf", itemName)
            };
        }

        return mapped.TryGetValue(storeName, out var result)
            ? result
            : Unavailable(storeName, itemName);
    }

    private async Task<ItemPriceResultDto?> FetchFromAggregatorAsync(string itemName, string? sku)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return null;

        try
        {
            var url = $"{AggregatorBaseUrl}?item={Uri.EscapeDataString(itemName)}";
            if (!string.IsNullOrWhiteSpace(sku))
            {
                url += $"&sku={Uri.EscapeDataString(sku)}";
            }

            return await _http.GetFromJsonAsync<ItemPriceResultDto>(url, JsonOptions);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[InventoryPricing] Aggregator request failed for '{itemName}' ({AggregatorBaseUrl})", ex);
            return null;
        }
    }

    private static Dictionary<string, PriceResult> MapStoreResults(IEnumerable<StorePriceDto>? prices, string itemName)
    {
        var map = new Dictionary<string, PriceResult>(StringComparer.OrdinalIgnoreCase);

        if (prices != null)
        {
            foreach (var price in prices)
            {
                if (string.IsNullOrWhiteSpace(price.StoreName))
                    continue;

                map[price.StoreName] = new PriceResult
                {
                    StoreName = price.StoreName,
                    Status = MapStatus(price.Status),
                    PriceKeys = price.PriceKeys.HasValue ? $"{price.PriceKeys.Value:0.##} keys" : string.Empty,
                    PriceRef = price.PriceUsd.HasValue ? $"${price.PriceUsd.Value:0.00}" : string.Empty,
                    ListingUrl = string.IsNullOrWhiteSpace(price.ListingUrl)
                        ? GetStoreSearchUrl(price.StoreName, itemName)
                        : price.ListingUrl
                };
            }
        }

        foreach (var store in StoreOrder)
        {
            if (!map.ContainsKey(store))
            {
                map[store] = Unavailable(store, itemName);
            }
        }

        return map;
    }

    private static string MapStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "Unavailable";

        return status switch
        {
            "Live" => "Live",
            "Approx" => "Approx via fallback",
            _ => "Unavailable"
        };
    }

    private static PriceSnapshot BuildUnavailableSnapshot(string itemName)
    {
        var map = new Dictionary<string, PriceResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var store in StoreOrder)
        {
            map[store] = Unavailable(store, itemName);
        }

        return new PriceSnapshot
        {
            StoreResults = map,
            PureSummary = "unavailable"
        };
    }

    private static bool AreAllStoresUnavailable(Dictionary<string, PriceResult> map)
        => map.Values.All(v => string.IsNullOrWhiteSpace(v.PriceRef) && string.IsNullOrWhiteSpace(v.PriceKeys));

    private static bool TryGetLocalApproxUsd(string itemName, out decimal usd)
    {
        usd = 0m;
        var normalized = itemName.Trim();

        if (normalized.Equals("Scrap Metal", StringComparison.OrdinalIgnoreCase))
        {
            usd = 0.03m;
            return true;
        }

        if (normalized.Equals("Reclaimed Metal", StringComparison.OrdinalIgnoreCase))
        {
            usd = 0.09m;
            return true;
        }

        if (normalized.Equals("Refined Metal", StringComparison.OrdinalIgnoreCase))
        {
            usd = 0.27m;
            return true;
        }

        if (normalized.Equals("Mann Co. Supply Crate Key", StringComparison.OrdinalIgnoreCase))
        {
            usd = 1.89m;
            return true;
        }

        return false;
    }

    private static PriceResult Unavailable(string storeName, string itemName) => new()
    {
        StoreName = storeName,
        Status = "Unavailable",
        ListingUrl = GetStoreSearchUrl(storeName, itemName)
    };

    public static string GetStoreSearchUrl(string storeName, string itemName)
    {
        var q = Uri.EscapeDataString(itemName);
        return storeName switch
        {
            "prices.tf" => $"https://prices.tf/items?q={q}",
            "Steam Market" => $"https://steamcommunity.com/market/search?appid=440&q={q}",
            "backpack.tf" => $"https://backpack.tf/classifieds?item={q}&quality=6&tradable=1&craftable=1",
            "marketplace.tf" => $"https://marketplace.tf/?query={q}",
            "mannco.store" => $"https://mannco.store/tf2?search={q}",
            "stntrading.eu" => $"https://stntrading.eu/tf2/items?search={q}",
            "tradeit.gg" => $"https://tradeit.gg/tf2/store?search={q}",
            _ => $"https://www.google.com/search?q={q}"
        };
    }

    public sealed class ApiKeys
    {
        public string? BackpackTfApiKey { get; set; }
        public string? MarketplaceTfApiKey { get; set; }
        public string? StnTradingApiKey { get; set; }
    }

    public sealed class PriceSnapshot
    {
        public Dictionary<string, PriceResult> StoreResults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string PureSummary { get; set; } = "Pure: unavailable";
    }

    public sealed class PriceResult
    {
        public string StoreName { get; set; } = string.Empty;
        public string Status { get; set; } = "Unavailable";
        public string PriceKeys { get; set; } = string.Empty;
        public string PriceRef { get; set; } = string.Empty;
        public string ListingUrl { get; set; } = string.Empty;
        public string FallbackUrl { get; set; } = string.Empty;
    }

    private sealed class ItemPriceResultDto
    {
        public string ItemName { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public List<StorePriceDto> Prices { get; set; } = [];
    }

    private sealed class StorePriceDto
    {
        public string StoreName { get; set; } = string.Empty;
        public string Status { get; set; } = "Unavailable";
        public decimal? PriceUsd { get; set; }
        public decimal? PriceKeys { get; set; }
        public string ListingUrl { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
