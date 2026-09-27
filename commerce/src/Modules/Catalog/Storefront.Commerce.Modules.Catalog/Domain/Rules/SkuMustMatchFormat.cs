namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C1: a SKU is upper-case letters, digits and hyphens, three to thirty-two characters.</summary>
public sealed class SkuMustMatchFormat(string text) : CatalogRule("SKU_INVALID", "catalog.sku_invalid")
{
    public override bool IsBroken() => !Sku.IsValid(text);
}
