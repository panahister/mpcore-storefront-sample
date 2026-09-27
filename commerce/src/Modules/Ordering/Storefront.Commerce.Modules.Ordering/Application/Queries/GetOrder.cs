using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Ordering.Application.Queries;

/// <summary>One order. A shopper reads only their own; another shopper's order is "not found", never "forbidden".</summary>
public sealed record GetOrder(Guid OrderId) : IQuery<Result<OrderView>>;

public static class GetOrderHandler
{
    public static async Task<Result<OrderView>> Handle(
        GetOrder query, ICurrentActorAccessor actor, IOrderReadModel orders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(orders);

        var found = await orders.FindAsync(new OrderId(query.OrderId), cancellationToken).ConfigureAwait(false);
        return found is null || !OrderAccess.MayRead(actor.Current, found.BuyerId)
            ? Result<OrderView>.FromFailure(OrderingFailures.OrderNotFound())
            : Result<OrderView>.Success(found.Order);
    }
}
