using System.Globalization;
using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Catalog.Domain;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Domain.Rules;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>Reprices a product. The reason goes to the audit trail; the new price goes to every basket, through Kafka.</summary>
public sealed record ChangeProductPrice(string Sku, decimal NewPrice, string Reason) : ICommand<Result<ProductStockView>>;

public static class ChangeProductPriceHandler
{
    public static async Task<Result<ProductStockView>> Handle(
        ChangeProductPrice command,
        IProductRepository products,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await products.FindBySkuAsync(Sku.Parse(command.Sku), cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return Result<ProductStockView>.FromFailure(CatalogFailures.ProductNotFound(Sku.Normalize(command.Sku)));
        }

        var oldPrice = product.Price;
        try
        {
            product.ChangePrice(Price.Of(command.NewPrice), clock.UtcNow);
        }
        catch (BusinessRuleValidationException refused)
        {
            // Refused by a rule, so nothing changed. Still worth a trace: a refused repricing is exactly what a
            // reviewer asks about later. RecordAttemptAsync writes detached from this transaction, so the record
            // survives although the transaction rolls back with the rethrown rule.
            await audit.RecordAttemptAsync(
                "catalog", "price-change", AuditOutcome.Rejected,
                new AuditFailure(refused.Rule.ErrorDomain, refused.Rule.Code),
                reason: command.Reason, entityType: nameof(Product), entityId: product.Sku.Value,
                metadata: new Dictionary<string, string>
                {
                    ["current_price"] = oldPrice.Amount.ToString(CultureInfo.InvariantCulture),
                    ["requested_price"] = command.NewPrice.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            throw;
        }

        await audit.RecordAsync(
            "catalog", "price-changed", nameof(Product), product.Sku.Value,
            new Dictionary<string, string>
            {
                ["old_price"] = oldPrice.Amount.ToString(CultureInfo.InvariantCulture),
                ["new_price"] = product.Price.Amount.ToString(CultureInfo.InvariantCulture),
                ["price_version"] = product.PriceVersion.ToString(CultureInfo.InvariantCulture),
                ["reason"] = command.Reason.Trim()
            },
            cancellationToken).ConfigureAwait(false);
        return Result<ProductStockView>.Success(ProductViews.Stock(product));
    }
}
