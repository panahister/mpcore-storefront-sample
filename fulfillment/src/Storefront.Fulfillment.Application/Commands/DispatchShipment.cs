using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Persistence.Abstractions;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Application.Views;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Application.Commands;

/// <summary>The warehouse hands the parcel of an order to a carrier.</summary>
public sealed record DispatchShipment(Guid OrderId, string Carrier, string TrackingCode) : ICommand<Result<ShipmentView>>;

/// <summary>
/// Marks the shipment dispatched. The aggregate raises <c>ShipmentDispatched</c>, which leaves for the shop
/// with the commit of this change, and not before (the transactional outbox).
/// </summary>
public static class DispatchShipmentHandler
{
    public static async Task<Result<ShipmentView>> Handle(
        DispatchShipment command,
        IShipmentRepository shipments,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(audit);

        var shipment = await shipments.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (shipment is null)
        {
            return Result<ShipmentView>.FromFailure(FulfillmentFailures.ShipmentNotFound());
        }

        // A shipment that has already left breaks rule F1; gRPC reports it as FailedPrecondition with the rule's code.
        shipment.Dispatch(command.Carrier.Trim(), command.TrackingCode.Trim(), clock.UtcNow);

        await audit.RecordAsync(
            "fulfillment", "shipment-dispatched", nameof(Shipment), shipment.OrderNumber,
            new Dictionary<string, string> { ["carrier"] = shipment.Carrier!, ["tracking_code"] = shipment.TrackingCode! },
            cancellationToken).ConfigureAwait(false);
        return Result<ShipmentView>.Success(ShipmentViews.Of(shipment));
    }
}
