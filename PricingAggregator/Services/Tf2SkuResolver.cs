namespace PricingAggregator.Services;

public static class Tf2SkuResolver
{
    private static readonly Dictionary<string, string> KnownSkus = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scrap Metal"] = "5000;6",
        ["Reclaimed Metal"] = "5001;6",
        ["Refined Metal"] = "5002;6",
        ["Mann Co. Supply Crate Key"] = "5021;6"
    };

    public static string? Resolve(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return null;

        return KnownSkus.TryGetValue(itemName.Trim(), out var sku)
            ? sku
            : null;
    }
}
