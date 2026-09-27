using Storefront.Commerce.Modules.Basket.Application.Ports;
using Microsoft.EntityFrameworkCore;
using BasketAggregate = Storefront.Commerce.Modules.Basket.Domain.Basket;

namespace Storefront.Commerce.Modules.Basket.Infrastructure;

/// <summary>The adapter behind <see cref="IBasketRepository"/>, generic over the host's context so Wolverine can build it inline.</summary>
public sealed class BasketRepository<TContext>(TContext database) : IBasketRepository where TContext : DbContext
{
    private DbSet<BasketAggregate> Baskets => database.Set<BasketAggregate>();

    public async Task<BasketAggregate?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        await Baskets.FirstOrDefaultAsync(b => b.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<BasketAggregate>> FindContainingAsync(string sku, CancellationToken cancellationToken) =>
        await Baskets.Where(b => b.Lines.Any(l => l.Id == sku)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(BasketAggregate aggregate) => Baskets.Add(aggregate);

    public void Remove(BasketAggregate aggregate) => Baskets.Remove(aggregate);
}
