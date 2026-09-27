using System.Globalization;

namespace Storefront.Commerce.Modules.Basket.Domain.Rules;

/// <summary>Rule B2: a line holds one to ten units. Bulk buying is a different business.</summary>
public sealed class QuantityMustBeWithinLineLimit(int quantity) : BasketRule(
    "QUANTITY_OUT_OF_RANGE", "basket.quantity_out_of_range",
    new Dictionary<string, string>
    {
        ["min"] = "1",
        ["max"] = Basket.MaximumQuantityPerLine.ToString(CultureInfo.InvariantCulture)
    })
{
    public override bool IsBroken() => quantity is < 1 or > Basket.MaximumQuantityPerLine;
}
