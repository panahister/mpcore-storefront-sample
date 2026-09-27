using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Ports;

/// <summary>The product store, as the application needs it: a port (Alistair Cockburn's Ports and Adapters).</summary>
public interface IProductRepository : IRepository<Product, ProductId>
{
    Task<Product?> FindBySkuAsync(Sku sku, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> FindBySkusAsync(IReadOnlyCollection<Sku> skus, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken);
}
