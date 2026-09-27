using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;

namespace Storefront.Commerce.Modules.Ordering.Infrastructure;

public sealed class OrderRepository<TContext>(TContext database) : IOrderRepository where TContext : DbContext
{
    public async Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken = default) =>
        await database.Set<Order>().FirstOrDefaultAsync(o => o.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(Order aggregate) => database.Set<Order>().Add(aggregate);

    public void Remove(Order aggregate) => database.Set<Order>().Remove(aggregate);
}
