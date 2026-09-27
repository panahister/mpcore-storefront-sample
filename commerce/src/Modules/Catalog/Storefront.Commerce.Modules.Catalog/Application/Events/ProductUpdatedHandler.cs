using Storefront.Commerce.Modules.Catalog.Application.Queries;
using Storefront.Commerce.Modules.Catalog.Domain.Events;
using MPCore.Caching.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Events;

/// <summary>Evicts the product's storefront view after any change commits. Idempotent.</summary>
public static class ProductUpdatedHandler
{
    public static Task Handle(ProductUpdated @event, ICache cache, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(cache);
        return cache.RemoveAsync(GetProductDetailsHandler.CacheKey(@event.Sku), cancellationToken);
    }
}
