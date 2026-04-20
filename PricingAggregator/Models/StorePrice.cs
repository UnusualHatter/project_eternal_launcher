namespace PricingAggregator.Models;

public sealed record StorePrice(
    string StoreName,
    PriceStatus Status,
    decimal? PriceUsd,
    decimal? PriceKeys,
    string ListingUrl,
    string Source,
    DateTimeOffset UpdatedAt
);
