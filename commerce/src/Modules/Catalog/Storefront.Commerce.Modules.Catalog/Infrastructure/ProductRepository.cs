using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Storefront.Commerce.Modules.Catalog.Infrastructure;

/// <summary>The adapter behind <see cref="IProductRepository"/>, generic over the host's context so Wolverine can build it inline.</summary>
public sealed class ProductRepository<TContext>(TContext database) : IProductRepository where TContext : DbContext
{
    private DbSet<Product> Products => database.Set<Product>();

    public async Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken = default) =>
        await Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<Product?> FindBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        await Products.FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Product>> FindBySkusAsync(IReadOnlyCollection<Sku> skus, CancellationToken cancellationToken)
    {
        var wanted = skus.ToList();
        return await Products.Where(p => wanted.Contains(p.Sku)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken) =>
        Products.AnyAsync(p => p.Sku == sku, cancellationToken);

    public void Add(Product aggregate) => Products.Add(aggregate);

    public void Remove(Product aggregate) => Products.Remove(aggregate);
}
