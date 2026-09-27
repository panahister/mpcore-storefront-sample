namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C12: stock moves in whole positive units.</summary>
public sealed class QuantityMustBePositive(int quantity) : CatalogRule("QUANTITY_NOT_POSITIVE", "catalog.quantity_not_positive")
{
    public override bool IsBroken() => quantity <= 0;
}
