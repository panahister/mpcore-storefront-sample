using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

public sealed class RestockAlertRepository<TContext>(TContext database) : IRestockAlertRepository where TContext : DbContext
{
    public async Task<RestockAlert?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await database.Set<RestockAlert>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

    public void Add(RestockAlert aggregate) => database.Set<RestockAlert>().Add(aggregate);

    public void Remove(RestockAlert aggregate) => database.Set<RestockAlert>().Remove(aggregate);
}
