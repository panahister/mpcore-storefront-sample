using System.Globalization;
using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>Receives a delivery into the warehouse.</summary>
/// <param name="Reference">The delivery note or purchase order. With the SKU it names the delivery (rule C14).</param>
/// <remarks>
/// Idempotent through the <see cref="StockReceipt"/>: the same delivery note for the same product adds its
/// units once, however often it is reported, and answers the current stock. The same note with another
/// quantity is not a repeat but a contradiction, and breaks rule C14.
/// </remarks>
public sealed record RestockProduct(string Sku, int Quantity, string Reference) : ICommand<Result<ProductStockView>>;

public static class RestockProductHandler
{
    public static async Task<Result<ProductStockView>> Handle(
        RestockProduct command,
        IProductRepository products,
        IStockReceiptRepository receipts,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var sku = Sku.Parse(command.Sku);
        var product = await products.FindBySkuAsync(sku, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result<ProductStockView>.FromFailure(CatalogFailures.ProductNotFound(sku.Value));
        }

        var reference = StockReceipt.NormalizeReference(command.Reference);
        var received = await receipts.FindAsync(sku, reference, cancellationToken).ConfigureAwait(false);
        if (received is not null)
        {
            // This delivery is already in the warehouse. Answer what is there now; change nothing.
            received.ConfirmRepeated(command.Quantity);
            return Result<ProductStockView>.Success(ProductViews.Stock(product));
        }

        product.Restock(command.Quantity);
        receipts.Add(StockReceipt.Record(sku, reference, command.Quantity, clock.UtcNow));

        await audit.RecordAsync(
            "catalog", "restocked", nameof(Product), product.Sku.Value,
            new Dictionary<string, string>
            {
                ["quantity"] = command.Quantity.ToString(CultureInfo.InvariantCulture),
                ["on_hand_after"] = product.OnHand.ToString(CultureInfo.InvariantCulture),
                ["reference"] = reference
            },
            cancellationToken).ConfigureAwait(false);
        return Result<ProductStockView>.Success(ProductViews.Stock(product));
    }
}
