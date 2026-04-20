using Microsoft.Extensions.Caching.Memory;
using PricingAggregator.Models;

namespace PricingAggregator.Services;

public sealed class PricingAggregatorService(IEnumerable<IPricingSource> sources, IMemoryCache cache)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(5);

    public async Task<ItemPriceResult> GetPricesAsync(string itemName, string? sku, CancellationToken ct = default)
    {
        var cacheKey = $"price:{sku ?? itemName}";
        if (cache.TryGetValue(cacheKey, out ItemPriceResult? cached) && cached is not null)
            return cached;

        var tasks = sources.Select(async source =>
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(SourceTimeout);

            try
            {
                return await source.GetPriceAsync(itemName, sku, timeoutCts.Token);
            }
            catch
            {
                return new StorePrice(
                    source.StoreName,
                    PriceStatus.Unavailable,
                    null,
                    null,
                    "#",
                    "timeout",
                    DateTimeOffset.UtcNow);
            }
        });

        var prices = await Task.WhenAll(tasks);
        var result = new ItemPriceResult(itemName, sku, prices, DateTimeOffset.UtcNow);

        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }
}
