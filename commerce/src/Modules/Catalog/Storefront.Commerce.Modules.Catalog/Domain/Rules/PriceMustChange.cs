namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C13: repricing to the same price is a mistake, not a change.</summary>
public sealed class PriceMustChange(Price current, Price requested) : CatalogRule("PRICE_UNCHANGED", "catalog.price_unchanged")
{
    public override bool IsBroken() => current == requested;
}
