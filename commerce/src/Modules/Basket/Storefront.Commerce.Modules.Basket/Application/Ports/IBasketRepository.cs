using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Basket.Application.Ports;

/// <summary>The basket store, as the application needs it (a port, in Alistair Cockburn's terms).</summary>
public interface IBasketRepository : IRepository<Domain.Basket, string>
{
    /// <summary>Every basket holding the product: a price change fans out to all of them.</summary>
    Task<IReadOnlyList<Domain.Basket>> FindContainingAsync(string sku, CancellationToken cancellationToken);
}
