using MPCore.Application.Querying;
using Storefront.Fulfillment.Application.Views;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Application.Ports;

/// <summary>The read side: views out, never the aggregate.</summary>
public interface IShipmentReadModel
{
    Task<ShipmentView?> FindAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Page<ShipmentSummary>> ListAsync(ShipmentStatus? status, PageRequest page, CancellationToken cancellationToken);
}
