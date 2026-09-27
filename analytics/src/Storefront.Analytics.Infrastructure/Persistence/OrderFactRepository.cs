using Microsoft.EntityFrameworkCore;
using Storefront.Analytics.Application.Ports;
using Storefront.Analytics.Application.Views;
using Storefront.Analytics.Domain;

namespace Storefront.Analytics.Infrastructure.Persistence;

public sealed class OrderFactRepository(AppDbContext database) : IOrderFactRepository
{
    public async Task<OrderFact?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<OrderFact>().FirstOrDefaultAsync(f => f.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(OrderFact aggregate) => database.Set<OrderFact>().Add(aggregate);

    public void Remove(OrderFact aggregate) => database.Set<OrderFact>().Remove(aggregate);
}
