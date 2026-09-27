using System.Globalization;
using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Audit;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>Lists a new product with its first stock. Catalog managers only.</summary>
public sealed record ListProduct(
    string Sku, string Name, string Description, string Category, string Brand,
    decimal Price, int InitialStock, int ReorderThreshold) : ICommand<Result<ProductStockView>>;

public static class ListProductHandler
{
    public static async Task<Result<ProductStockView>> Handle(
        ListProduct command,
        IProductRepository products,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The request's shape was checked by ListProductValidator before this ran. What follows are the
        // business rules: the value objects and the aggregate check them and throw the broken one, which
        // the edge reports as 422 under the rule's own code. Nothing is tracked until every rule passed
        // (ADR-011 §4: validate first, mutate second).
        var sku = Sku.Parse(command.Sku);
        if (await products.SkuExistsAsync(sku, cancellationToken).ConfigureAwait(false))
        {
            return Result<ProductStockView>.FromFailure(CatalogFailures.SkuTaken(sku));
        }

        var product = Product.List(
            ProductId.New(), sku, command.Name, command.Description, command.Category, command.Brand,
            Price.Of(command.Price), command.InitialStock, command.ReorderThreshold);

        products.Add(product);
        await audit.RecordAsync(
            "catalog", "product-listed", nameof(Product), product.Sku.Value,
            new Dictionary<string, string>
            {
                ["price"] = product.Price.Amount.ToString(CultureInfo.InvariantCulture),
                ["initial_stock"] = product.OnHand.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken).ConfigureAwait(false);
        return Result<ProductStockView>.Success(ProductViews.Stock(product));
    }
}
