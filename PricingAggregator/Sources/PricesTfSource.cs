using PricingAggregator.Models;
using PricingAggregator.Services;

namespace PricingAggregator.Sources;

public sealed class PricesTfSource(HttpClient http) : IPricingSource
{
    public string StoreName => "prices.tf";

    public async Task<StorePrice> GetPriceAsync(string itemName, string? sku, CancellationToken ct)
    {
        var resolvedSku = string.IsNullOrWhiteSpace(sku) ? Tf2SkuResolver.Resolve(itemName) : sku;
        if (string.IsNullOrWhiteSpace(resolvedSku))
            return Unavailable(itemName, "missing-sku");

        try
        {
            var url = $"https://api2.prices.tf/prices/{Uri.EscapeDataString(resolvedSku)}";
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Unavailable(itemName, "api");

            var payload = await response.Content.ReadFromJsonAsync<PricesTfResponse>(cancellationToken: ct);
            if (payload == null)
                return Unavailable(itemName, "api");

            var usd = payload.SellPrice > 0 ? payload.SellPrice / 100m : (decimal?)null;

            return new StorePrice(
                StoreName,
                PriceStatus.Live,
                usd,
                null,
                $"https://prices.tf/items/{Uri.EscapeDataString(resolvedSku)}",
                "api",
                DateTimeOffset.UtcNow);
        }
        catch
        {
            return Unavailable(itemName, "timeout");
        }
    }

    private StorePrice Unavailable(string itemName, string source)
        => new(
            StoreName,
            PriceStatus.Unavailable,
            null,
            null,
            $"https://prices.tf/items?q={Uri.EscapeDataString(itemName)}",
            source,
            DateTimeOffset.UtcNow);

    private sealed record PricesTfResponse(int SellPrice, int BuyPrice);
}
