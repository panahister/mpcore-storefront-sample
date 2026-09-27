namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C5: a discontinued product is final; it is never sold, repriced or restocked again.</summary>
public sealed class ProductMustBeActive(Product product) : CatalogRule(
    "PRODUCT_DISCONTINUED", "catalog.product_discontinued",
    new Dictionary<string, string> { ["sku"] = product.Sku.Value })
{
    public override bool IsBroken() => product.Status == ProductStatus.Discontinued;
}
