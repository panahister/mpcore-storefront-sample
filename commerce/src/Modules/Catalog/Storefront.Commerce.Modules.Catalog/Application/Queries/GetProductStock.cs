using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;

namespace Storefront.Commerce.Modules.Catalog.Application.Queries;

/// <summary>The warehouse numbers of one product. Catalog managers only. Reads through the read model, like every query.</summary>
public sealed record GetProductStock(string Sku) : IQuery<Result<ProductStockView>>;

public static class GetProductStockHandler
{
    public static async Task<Result<ProductStockView>> Handle(
        GetProductStock query, ICatalogReadModel catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!Sku.TryParse(query.Sku, out var sku))
        {
            return Result<ProductStockView>.FromFailure(CatalogFailures.ProductNotFound(Sku.Normalize(query.Sku)));
        }

        var stock = await catalog.FindStockAsync(sku, cancellationToken).ConfigureAwait(false);
        return stock is null
            ? Result<ProductStockView>.FromFailure(CatalogFailures.ProductNotFound(sku.Value))
            : Result<ProductStockView>.Success(stock);
    }
}
