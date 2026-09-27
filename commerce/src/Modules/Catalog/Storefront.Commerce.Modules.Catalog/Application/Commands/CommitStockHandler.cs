using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>Takes an order's held units out of the warehouse when it ships. Idempotent.</summary>
public static class CommitStockHandler
{
    public static async Task Handle(
        CommitStock command,
        IProductRepository products,
        IStockReservationRepository reservations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<CommitStock> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reservation = await reservations.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (reservation is null || !reservation.IsHeld)
        {
            logger.LogWarning("Commit for order {OrderId} found no held reservation ({Status}); nothing to do",
                command.OrderId, reservation?.Status.ToString() ?? "none");
            return;
        }

        var found = await products.FindBySkusAsync(
            [.. reservation.Lines.Select(static l => Sku.FromTrusted(l.Sku))], cancellationToken).ConfigureAwait(false);
        foreach (var line in reservation.Lines)
        {
            found.Single(p => p.Sku.Value == line.Sku).CommitShipment(line.Quantity);
        }

        reservation.MarkCommitted(clock.UtcNow);
    }
}
