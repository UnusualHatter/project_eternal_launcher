using PricingAggregator.Models;

namespace PricingAggregator.Services;

public interface IPricingSource
{
    string StoreName { get; }

    Task<StorePrice> GetPriceAsync(string itemName, string? sku, CancellationToken ct);
}
