using System.Globalization;

namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>Rule C2: above the ceiling the number is a typing mistake, not a price.</summary>
public sealed class PriceMustNotExceedCeiling(decimal amount) : CatalogRule(
    "PRICE_ABOVE_CEILING", "catalog.price_above_ceiling",
    new Dictionary<string, string> { ["ceiling"] = Price.Ceiling.ToString("0", CultureInfo.InvariantCulture) })
{
    public override bool IsBroken() => amount > Price.Ceiling;
}
