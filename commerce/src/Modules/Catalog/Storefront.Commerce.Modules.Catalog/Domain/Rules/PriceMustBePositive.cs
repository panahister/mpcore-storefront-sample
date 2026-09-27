namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C2: a price is more than nothing.</summary>
public sealed class PriceMustBePositive(decimal amount) : CatalogRule("PRICE_NOT_POSITIVE", "catalog.price_not_positive")
{
    public override bool IsBroken() => amount <= 0m;
}

/// <summary>Rule C2: a price is an amount of money, so it has cents and nothing smaller.</summary>
public sealed class PriceMustBeInCents(decimal amount) : CatalogRule("PRICE_TOO_PRECISE", "catalog.price_too_precise")
{
    public override bool IsBroken() => decimal.Round(amount, 2) != amount;
}
