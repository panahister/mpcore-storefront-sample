namespace Storefront.Commerce.Modules.Ordering.Domain.Rules;

/// <summary>Rule O5: a shipped order can no longer be cancelled; that would be a return, a different business.</summary>
public sealed class OrderMustNotBeShipped(Order order) : OrderingRule("ALREADY_SHIPPED", "ordering.already_shipped")
{
    public override bool IsBroken() => order.Status == OrderStatus.Shipped;
}
