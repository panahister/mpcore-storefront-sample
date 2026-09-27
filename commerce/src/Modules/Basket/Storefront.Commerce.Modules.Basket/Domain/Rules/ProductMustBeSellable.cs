namespace Storefront.Commerce.Modules.Basket.Domain.Rules;

/// <summary>Rule B5: only a product the Catalog says is for sale can be put in a basket.</summary>
public sealed class ProductMustBeSellable(string sku, bool sellable) : BasketRule(
    "PRODUCT_NOT_SELLABLE", "basket.product_not_sellable",
    new Dictionary<string, string> { ["sku"] = sku })
{
    public override bool IsBroken() => !sellable;
}
