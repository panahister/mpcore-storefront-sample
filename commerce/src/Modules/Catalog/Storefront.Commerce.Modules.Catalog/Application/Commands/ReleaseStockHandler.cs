using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>Returns an order's held units to the shelf. Idempotent, and safe to arrive before the reservation it undoes.</summary>
public static class ReleaseStockHandler
{
    public static async Task Handle(
        ReleaseStock command,
        IProductRepository products,
        IStockReservationRepository reservations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ReleaseStock> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reservation = await reservations.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        if (reservation is null)
        {
            // The order was cancelled before its ReserveStock was processed. Remember that, so the
            // request arriving later is answered "rejected" instead of holding units for a cancelled order.
            reservations.Add(StockReservation.Void(command.OrderId, now));
            logger.LogInformation("Release for order {OrderId} arrived before its reservation; voided it", command.OrderId);
            return;
        }

        if (!reservation.IsHeld)
        {
            return;
        }

        var found = await products.FindBySkusAsync(
            [.. reservation.Lines.Select(static l => Sku.FromTrusted(l.Sku))], cancellationToken).ConfigureAwait(false);
        foreach (var line in reservation.Lines)
        {
            found.Single(p => p.Sku.Value == line.Sku).Release(line.Quantity);
        }

        reservation.MarkReleased(now);
        logger.LogInformation("Stock for order {OrderId} released", command.OrderId);
    }
}
