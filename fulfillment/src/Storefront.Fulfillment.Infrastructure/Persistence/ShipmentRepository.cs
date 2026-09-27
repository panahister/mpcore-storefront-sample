using Microsoft.EntityFrameworkCore;
using MPCore.Application.Querying;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Application.Views;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Infrastructure.Persistence;

public sealed class ShipmentRepository(AppDbContext database) : IShipmentRepository
{
    public async Task<Shipment?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<Shipment>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(Shipment aggregate) => database.Set<Shipment>().Add(aggregate);

    public void Remove(Shipment aggregate) => database.Set<Shipment>().Remove(aggregate);
}
