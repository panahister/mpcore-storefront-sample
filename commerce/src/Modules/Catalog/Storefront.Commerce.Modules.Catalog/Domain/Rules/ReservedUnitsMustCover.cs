namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C6: units are released or shipped only out of what was reserved.</summary>
public sealed class ReservedUnitsMustCover(Product product, int quantity) : CatalogRule(
    "RESERVATION_MISMATCH", "catalog.reservation_mismatch",
    new Dictionary<string, string> { ["sku"] = product.Sku.Value })
{
    public override bool IsBroken() => quantity > product.Reserved || quantity > product.OnHand;
}
