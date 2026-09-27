using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Catalog.Domain;
using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Commands;

/// <summary>
/// Holds the stock of an order, all lines or none, and answers Ordering. Runs from a durable local queue.
/// </summary>
/// <remarks>
/// Idempotent through the <see cref="StockReservation"/>, whose identity is the order's: a redelivered request
/// finds the decision already taken and repeats the answer. Hohpe and Woolf's <i>Idempotent Receiver</i>.
/// </remarks>
public static class ReserveStockHandler
{
    private sealed record RequestedLine(string Text, Sku? Sku, int Quantity);

    public static async Task Handle(
        ReserveStock command,
        IProductRepository products,
        IStockReservationRepository reservations,
        IMessagePublisher publisher,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ReserveStock> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(publisher);

        var existing = await reservations.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            // Redelivery, or a release that overtook this request. Repeat the decision; change nothing.
            logger.LogInformation("Stock for order {OrderId} was already decided ({Status}); repeating the answer",
                command.OrderId, existing.Status);
            await publisher.PublishAsync(
                existing.Status is ReservationStatus.Held or ReservationStatus.Committed
                    ? new StockReserved(command.OrderId)
                    : new StockReservationRejected(command.OrderId, []),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        // One line per SKU, whatever the caller sent. A SKU the Catalog cannot even parse is a shortage.
        var lines = command.Lines
            .GroupBy(static l => Sku.Normalize(l.Sku), StringComparer.Ordinal)
            .Select(static g => new RequestedLine(g.Key, Sku.TryParse(g.Key, out var sku) ? sku : null, g.Sum(static l => l.Quantity)))
            .ToList();
        var found = await products.FindBySkusAsync(
            [.. lines.Where(static l => l.Sku is not null).Select(static l => l.Sku!.Value)], cancellationToken).ConfigureAwait(false);
        var bySku = found.ToDictionary(static p => p.Sku.Value, StringComparer.Ordinal);

        var shortages = new List<StockShortage>();
        foreach (var line in lines)
        {
            if (!bySku.TryGetValue(line.Text, out var product))
            {
                shortages.Add(new StockShortage(line.Text, line.Quantity, 0));
            }
            else if (!product.CanReserve(line.Quantity))
            {
                shortages.Add(new StockShortage(line.Text, line.Quantity, product.IsSellable ? Math.Max(product.Available, 0) : 0));
            }
        }

        var now = clock.UtcNow;
        var reservationLines = lines.Select(static l => new ReservationLine(l.Text, l.Quantity)).ToList();
        if (shortages.Count > 0)
        {
            reservations.Add(StockReservation.Reject(command.OrderId, reservationLines, now));
            await publisher.PublishAsync(new StockReservationRejected(command.OrderId, shortages), cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation("Stock for order {OrderId} rejected: {ShortageCount} line(s) short",
                command.OrderId, shortages.Count);
            return;
        }

        // CanReserve said yes for every line on these very objects, so the rules the aggregate checks here
        // hold. If one broke anyway that is a bug, and the exception goes to the dead-letter queue.
        foreach (var line in lines)
        {
            bySku[line.Text].Reserve(line.Quantity);
        }

        reservations.Add(StockReservation.Hold(command.OrderId, reservationLines, now));
        await publisher.PublishAsync(new StockReserved(command.OrderId), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Stock for order {OrderId} reserved: {LineCount} line(s)", command.OrderId, lines.Count);
    }
}
