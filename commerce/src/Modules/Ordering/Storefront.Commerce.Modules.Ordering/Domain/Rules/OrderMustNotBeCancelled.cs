namespace Storefront.Commerce.Modules.Ordering.Domain.Rules;

/// <summary>Rule O5: a cancelled order is final.</summary>
public sealed class OrderMustNotBeCancelled(Order order) : OrderingRule("ALREADY_CANCELLED", "ordering.already_cancelled")
{
    public override bool IsBroken() => order.Status == OrderStatus.Cancelled;
}
