using System.Globalization;

namespace Storefront.Commerce.Modules.Catalog.Domain.Rules;

/// <summary>
/// Rule C3: one repricing moves the price by at most half. A guard against the fat finger, not a pricing
/// policy: 450,000 typed as 4,500,000 is caught here. A genuine large repricing is two steps, each of which
/// somebody looked at.
/// </summary>
public sealed class PriceMoveMustBeGradual(Price current, Price requested) : CatalogRule(
    "PRICE_JUMP_TOO_LARGE", "catalog.price_jump_too_large",
    new Dictionary<string, string>
    {
        ["current"] = current.Amount.ToString("0", CultureInfo.InvariantCulture),
        ["requested"] = requested.Amount.ToString("0", CultureInfo.InvariantCulture),
        ["max_move_percent"] = ((int)(Product.MaximumPriceMove * 100)).ToString(CultureInfo.InvariantCulture)
    })
{
    public override bool IsBroken() => Math.Abs(requested.Amount - current.Amount) > current.Amount * Product.MaximumPriceMove;
}
