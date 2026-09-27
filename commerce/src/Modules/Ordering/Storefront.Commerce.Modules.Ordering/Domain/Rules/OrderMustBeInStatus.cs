namespace Storefront.Commerce.Modules.Ordering.Domain.Rules;

/// <summary>Rule O5: an order moves along its life one step at a time; a step from the wrong place is refused.</summary>
public sealed class OrderMustBeInStatus(OrderStatus expected, OrderStatus actual) : OrderingRule(
    "ORDER_STATUS_MISMATCH", "ordering.status_mismatch",
    new Dictionary<string, string> { ["expected"] = expected.ToString(), ["actual"] = actual.ToString() })
{
    public override bool IsBroken() => actual != expected;
}
