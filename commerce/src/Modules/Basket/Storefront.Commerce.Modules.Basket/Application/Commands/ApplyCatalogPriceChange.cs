using Storefront.Commerce.Modules.Basket.Application.Ports;
using MPCore.Application.Messaging;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Basket.Application.Commands;

/// <summary>
/// The Basket's own reading of the Catalog's price event. The host's Kafka consumer translates the
/// integration event into this command, so the module never depends on the broker.
/// </summary>
public sealed record ApplyCatalogPriceChange(string Sku, decimal NewPrice, int PriceVersion) : ICommand;

public static class ApplyCatalogPriceChangeHandler
{
    public static async Task Handle(
        ApplyCatalogPriceChange command,
        IBasketRepository baskets,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var affected = await baskets.FindContainingAsync(command.Sku, cancellationToken).ConfigureAwait(false);
        foreach (var basket in affected)
        {
            basket.ApplyPriceChange(command.Sku, command.NewPrice, command.PriceVersion);
        }
    }
}
