using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Audit;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>Withdraws a product for good (rule C5).</summary>
public sealed record DiscontinueProduct(string Sku, string Reason) : ICommand<Result<ProductStockView>>;

public static class DiscontinueProductHandler
{
    public static async Task<Result<ProductStockView>> Handle(
        DiscontinueProduct command,
        IProductRepository products,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await products.FindBySkuAsync(Sku.Parse(command.Sku), cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result<ProductStockView>.FromFailure(CatalogFailures.ProductNotFound(Sku.Normalize(command.Sku)));
        }

        product.Discontinue();

        await audit.RecordAsync(
            "catalog", "discontinued", nameof(Product), product.Sku.Value,
            new Dictionary<string, string> { ["reason"] = command.Reason.Trim() },
            cancellationToken).ConfigureAwait(false);
        return Result<ProductStockView>.Success(ProductViews.Stock(product));
    }
}
