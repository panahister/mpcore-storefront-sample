using MPCore.Application.Messaging;
using MPCore.Application.Results;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Application.Views;

namespace Storefront.Fulfillment.Application.Queries;

public sealed record GetShipment(Guid OrderId) : IQuery<Result<ShipmentView>>;

public static class GetShipmentHandler
{
    public static async Task<Result<ShipmentView>> Handle(GetShipment query, IShipmentReadModel shipments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var shipment = await shipments.FindAsync(query.OrderId, cancellationToken).ConfigureAwait(false);
        return shipment is null
            ? Result<ShipmentView>.FromFailure(FulfillmentFailures.ShipmentNotFound())
            : Result<ShipmentView>.Success(shipment);
    }
}
