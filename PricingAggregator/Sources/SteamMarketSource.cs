using System.Globalization;
using PricingAggregator.Models;
using PricingAggregator.Services;

namespace PricingAggregator.Sources;

public sealed class SteamMarketSource(HttpClient http) : IPricingSource
{
    public string StoreName => "Steam Market";

    public async Task<StorePrice> GetPriceAsync(string itemName, string? sku, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(itemName);
            var url = $"https://steamcommunity.com/market/priceoverview/?appid=440&currency=1&market_hash_name={encoded}";

            using var response = await http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<SteamPriceResponse>(cancellationToken: ct);
                if (payload?.success == true && !string.IsNullOrWhiteSpace(payload.lowest_price) &&
                    TryParseUsd(payload.lowest_price, out var usd))
                {
                    return new StorePrice(
                        StoreName,
                        PriceStatus.Approx,
                        usd,
                        null,
                        $"https://steamcommunity.com/market/listings/440/{encoded}",
                        "fallback",
                        DateTimeOffset.UtcNow);
                }
            }

            var fallback = await TrySearchRenderFallbackAsync(itemName, ct);
            if (fallback is not null)
                return fallback;

            return Unavailable(itemName, "fallback");
        }
        catch
        {
            return Unavailable(itemName, "timeout");
        }
    }

    private static bool TryParseUsd(string raw, out decimal value)
    {
        value = 0m;

        var cleaned = raw
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("USD", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
               || decimal.TryParse(cleaned.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private StorePrice Unavailable(string itemName, string source)
        => new(
            StoreName,
            PriceStatus.Unavailable,
            null,
            null,
            $"https://steamcommunity.com/market/listings/440/{Uri.EscapeDataString(itemName)}",
            source,
            DateTimeOffset.UtcNow);

    private async Task<StorePrice?> TrySearchRenderFallbackAsync(string itemName, CancellationToken ct)
    {
        try
        {
            var query = Uri.EscapeDataString(itemName);
            var url = $"https://steamcommunity.com/market/search/render/?appid=440&norender=1&count=20&query={query}";
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<SteamSearchResponse>(cancellationToken: ct);
            var first = payload?.results?.FirstOrDefault();
            if (first == null)
                return null;

            var usd = first.sell_price > 0
                ? first.sell_price / 100m
                : 0m;

            if (usd <= 0m && !TryParseUsd(first.sell_price_text ?? string.Empty, out usd))
                return null;

            var hashName = string.IsNullOrWhiteSpace(first.hash_name) ? itemName : first.hash_name;
            return new StorePrice(
                StoreName,
                PriceStatus.Approx,
                usd,
                null,
                $"https://steamcommunity.com/market/listings/440/{Uri.EscapeDataString(hashName)}",
                "fallback",
                DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    private sealed record SteamPriceResponse(bool success, string? lowest_price);

    private sealed record SteamSearchResponse(List<SteamSearchResult>? results);

    private sealed record SteamSearchResult(string? hash_name, int sell_price, string? sell_price_text);
}
