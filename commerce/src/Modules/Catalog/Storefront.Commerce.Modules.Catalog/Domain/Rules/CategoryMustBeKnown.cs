namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C11: a product belongs to one of the store's categories.</summary>
public sealed class CategoryMustBeKnown(string? category) : CatalogRule(
    "CATEGORY_UNKNOWN", "catalog.category_unknown",
    new Dictionary<string, string> { ["category"] = Truncate(category) })
{
    public override bool IsBroken() => !ProductCategories.IsKnown(category);

    private static string Truncate(string? value) => value is null ? string.Empty : value.Length <= 64 ? value : value[..64];
}
