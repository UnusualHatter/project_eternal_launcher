namespace PricingAggregator.Models;

public sealed record ItemPriceResult(
    string ItemName,
    string? Sku,
    IReadOnlyList<StorePrice> Prices,
    DateTimeOffset ResolvedAt
);
