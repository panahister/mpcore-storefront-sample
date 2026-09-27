namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>The categories the store sells. A fixed list is a business decision, not a table.</summary>
public static class ProductCategories
{
    public static readonly IReadOnlyList<string> All =
        ["tents", "sleeping-bags", "backpacks", "footwear", "cooking", "clothing"];

    public static bool IsKnown(string? category) =>
        category is not null && All.Contains(category, StringComparer.Ordinal);
}
