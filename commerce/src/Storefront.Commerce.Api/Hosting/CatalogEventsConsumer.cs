using Storefront.Commerce.Modules.Basket.Application.Commands;
using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain.Events;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// The Kafka consumer seam between the Catalog and the Basket.
/// </summary>
/// <remarks>
/// <para>
/// The Basket must not reference the Catalog's Domain, where <see cref="ProductPriceChanged"/> lives,
/// and the Catalog must not know the Basket exists. The host is the only place that knows both, so the
/// host receives the integration event from Kafka and translates it into the Basket's own command.
/// Moving the Basket into its own service later means moving this one file with it.
/// </para>
/// <para>
/// The event arrives through Kafka even though producer and consumer share a process. That is the
/// point: the Basket sees exactly what any other Storefront service would see — at-least-once delivery,
/// possibly twice, possibly late — and the price version in the event is what makes that harmless.
/// </para>
/// </remarks>
public static class CatalogEventsConsumer
{
    /// <summary>Consumes <see cref="ProductPriceChanged"/>.</summary>
    public static async Task Handle(
        ProductPriceChanged message, IBasketRepository baskets, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await ApplyCatalogPriceChangeHandler.Handle(
            new ApplyCatalogPriceChange(message.Sku, message.NewPrice, message.PriceVersion),
            baskets, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
