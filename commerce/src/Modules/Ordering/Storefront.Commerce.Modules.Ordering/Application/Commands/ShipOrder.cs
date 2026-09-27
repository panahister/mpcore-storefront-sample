using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Ordering.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Ordering.Application.Commands;

/// <summary>The warehouse hands a paid order to a carrier. Reached over gRPC.</summary>
public sealed record ShipOrder(Guid OrderId, string Carrier, string TrackingCode) : ICommand<Result<OrderView>>;

public static class ShipOrderHandler
{
    public static async Task<Result<OrderView>> Handle(
        ShipOrder command,
        IOrderRepository orders,
        IMessagePublisher publisher,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(audit);

        var order = await orders.GetAsync(new OrderId(command.OrderId), cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result<OrderView>.FromFailure(OrderingFailures.OrderNotFound());
        }

        // An unpaid or cancelled order breaks rules O5 and O9 here; gRPC reports it as FailedPrecondition with the rule's code.
        order.Ship(command.Carrier.Trim(), command.TrackingCode.Trim(), clock.UtcNow);

        await publisher.PublishAsync(new CommitStock(order.Id.Value), cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            "ordering", "order-shipped", nameof(Order), order.OrderNumber,
            new Dictionary<string, string> { ["carrier"] = order.Carrier!, ["tracking_code"] = order.TrackingCode! },
            cancellationToken).ConfigureAwait(false);

        OrderingMetrics.Transitions.Add(1, new KeyValuePair<string, object?>("status", "shipped"));
        return Result<OrderView>.Success(OrderViews.Of(order));
    }
}
