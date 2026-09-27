using System.Globalization;

namespace Storefront.Commerce.Modules.Basket.Domain.Rules;

/// <summary>Rule B2: a basket holds at most twenty different products.</summary>
public sealed class BasketMustHaveRoomForAnotherLine(int currentLines) : BasketRule(
    "TOO_MANY_LINES", "basket.too_many_lines",
    new Dictionary<string, string> { ["max"] = Basket.MaximumLines.ToString(CultureInfo.InvariantCulture) })
{
    public override bool IsBroken() => currentLines >= Basket.MaximumLines;
}
