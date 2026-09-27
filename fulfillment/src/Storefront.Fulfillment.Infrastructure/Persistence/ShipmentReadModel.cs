using Microsoft.EntityFrameworkCore;
using MPCore.Application.Querying;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Application.Views;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Infrastructure.Persistence;

/// <summary>The read side: read without tracking, mapped to a view before it leaves.</summary>
public sealed class ShipmentReadModel(AppDbContext database) : IShipmentReadModel
{
    public async Task<ShipmentView?> FindAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var shipment = await database.Set<Shipment>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == orderId, cancellationToken).ConfigureAwait(false);
        return shipment is null ? null : ShipmentViews.Of(shipment);
    }

    public async Task<Page<ShipmentSummary>> ListAsync(ShipmentStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var query = database.Set<Shipment>().AsNoTracking();
        if (status is not null)
        {
            query = query.Where(s => s.Status == status);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(s => s.ReceivedOnUtc)
            .Skip(page.Skip).Take(page.Size)
            .Select(s => new ShipmentSummary(
                s.Id, s.OrderNumber, s.Status.ToString(), s.Address.City, s.Lines.Sum(l => l.Quantity), s.ReceivedOnUtc))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new Page<ShipmentSummary>(items, page.Number, page.Size, total);
    }
}
