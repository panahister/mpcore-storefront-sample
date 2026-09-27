using Storefront.Commerce.Modules.Ordering.Domain;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Ordering.Application;

/// <summary>Who may see or stop an order. Support and the warehouse see all; a shopper sees only their own.</summary>
public static class OrderAccess
{
    public static bool IsBuyer(CurrentActor caller, Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return IsBuyer(caller, order.BuyerId);
    }

    public static bool IsBuyer(CurrentActor caller, string buyerId)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.SubjectId is not null && string.Equals(caller.SubjectId, buyerId, StringComparison.Ordinal);
    }

    public static bool MayRead(CurrentActor caller, string buyerId)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.HasRole(OrderingRoles.Support) || caller.HasRole(OrderingRoles.Warehouse) || IsBuyer(caller, buyerId);
    }
}
