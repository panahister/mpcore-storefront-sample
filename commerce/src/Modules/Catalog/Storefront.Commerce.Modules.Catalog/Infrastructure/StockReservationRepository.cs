using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class StockReservationRepository<TContext>(TContext database) : IStockReservationRepository where TContext : DbContext
{
    public async Task<StockReservation?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<StockReservation>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(StockReservation aggregate) => database.Set<StockReservation>().Add(aggregate);

    public void Remove(StockReservation aggregate) => database.Set<StockReservation>().Remove(aggregate);
}
