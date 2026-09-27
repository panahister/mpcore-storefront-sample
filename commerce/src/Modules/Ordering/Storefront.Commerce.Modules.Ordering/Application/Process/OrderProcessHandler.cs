using Storefront.Commerce.Modules.Basket.Contracts;
using Storefront.Commerce.Modules.Catalog.Contracts;
using Storefront.Commerce.Modules.Ordering.Application.Ports;
using Storefront.Commerce.Modules.Ordering.Domain;
using Storefront.Commerce.Modules.Payments.Contracts;
using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Ordering.Application.Process;

/// <summary>
/// The order process: how a checkout becomes an order, and how the order moves from "submitted" to "paid",
/// or to "cancelled", as the Catalog and Payments modules answer.
/// </summary>
/// <remarks>
/// <para>
/// A <b>process manager</b> (Gregor Hohpe and Bobby Woolf, <i>Enterprise Integration Patterns</i>), also
/// called an orchestration-based saga, without a framework type: the state lives on the <see cref="Order"/>
/// itself. Each answer is one handler, one transition and at most one next command, all in one transaction.
/// This class chooses the next step; whether a step is allowed is the aggregate's rule.
/// </para>
/// <para>
/// <b>Every answer can arrive late, twice, or after the shopper cancelled.</b> The handler asks the order
/// first (<c>CanConfirmStock</c>, <c>CanCancel</c>) and ignores what no longer applies, instead of letting a
/// rule break: a broken rule on a queued message is a verdict and goes to the dead-letter queue. Stock
/// reserved for a cancelled order is released; a charge approved for a cancelled order is refunded. Nothing
/// is lost and nothing is charged twice, and each of those cases is a test.
/// </para>
/// <para>
/// <b>Every step changes one module.</b> Ordering writes orders and nothing else. What the Basket, the
/// Catalog and Payments have to do they are told in a message that commits with the order's change (the
/// transactional outbox), and each does it in a transaction of its own. Vaughn Vernon's aggregate rule
/// (<i>Implementing Domain-Driven Design</i>): one aggregate per transaction, eventual consistency between
/// them. The order of the steps is fixed so that no step waits for another: the charge is registered before
/// the warehouse is asked, so a payment exists by the time stock is held or the order is cancelled.
/// </para>
/// <para>
/// These handlers run from Wolverine's durable local queues, outside any HTTP request. MP Core names each
/// one as a system actor after its message (<c>system:StockReserved</c>) for the whole execution, the
/// middleware-owned save included, so the audit trail never records a transition as made by nobody.
/// </para>
/// </remarks>
public static class OrderProcessHandler
{
    /// <summary>The shopper checked out: the order exists from here on. Register its charge.</summary>
    public static async Task Handle(
        BasketCheckedOut message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        ILogger<BasketCheckedOut> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var id = new OrderId(message.OrderId);
        if (await orders.GetAsync(id, cancellationToken).ConfigureAwait(false) is not null)
        {
            // Redelivery. The order and its request to Payments committed together the first time.
            logger.LogInformation("Duplicate checkout for order {OrderId} ignored", message.OrderId);
            return;
        }

        // The Basket refused at the edge every address these value objects would refuse, and a test holds
        // the two together. A rule that breaks here anyway is a broken contract between two modules: the
        // message goes to the error queue for an operator, where a silent repair would hide it.
        var a = message.ShippingAddress;
        var address = new ShippingAddress(
            a.RecipientName, PhoneNumber.Parse(a.Phone), a.Province, a.City, a.Line, PostalCode.Parse(a.PostalCode));

        var order = Order.Place(
            id, message.BuyerId, message.BuyerName, address, message.Currency,
            [.. message.Lines.Select(static l => new OrderLine(l.Sku, l.ProductName, l.UnitPrice, l.Quantity))],
            message.CheckedOutOnUtc);
        orders.Add(order);

        await publisher.PublishAsync(
            new RegisterPayment(order.Id.Value, order.Total, order.Currency, message.PaymentIntentId, order.BuyerId), cancellationToken)
            .ConfigureAwait(false);

        OrderingMetrics.Placed.Add(1);
        OrderingMetrics.PlacedValue.Add((double)order.Total);
        logger.LogInformation("Order {OrderNumber} placed: {LineCount} line(s)", order.OrderNumber, order.Lines.Count);
    }

    /// <summary>The charge is registered: ask the warehouse for the stock.</summary>
    public static async Task Handle(
        PaymentRegistered message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        ILogger<PaymentRegistered> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        if (order.IsCancelled)
        {
            // Cancelled before its charge was registered: the cancellation found no payment to void.
            logger.LogInformation("Order {OrderNumber} was cancelled before its charge was registered; voiding it", order.OrderNumber);
            await publisher.PublishAsync(new VoidPayment(order.Id.Value), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!order.CanConfirmStock)
        {
            logger.LogInformation("Duplicate PaymentRegistered for {OrderNumber} ignored ({Status})", order.OrderNumber, order.Status);
            return;
        }

        // Nothing changes on the order: it stays "submitted" until the warehouse answers. A repeat asks
        // again, and the Catalog, which remembers its decision by the order, repeats its answer.
        await publisher.PublishAsync(
            new ReserveStock(order.Id.Value, [.. order.Lines.Select(static l => new StockLine(l.Sku, l.Quantity))]),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The warehouse holds the stock: ask for the charge.</summary>
    public static async Task Handle(
        StockReserved message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        IClock clock, ILogger<StockReserved> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        if (order.IsCancelled)
        {
            // Cancelled while the reservation was in flight: the units must go straight back.
            logger.LogInformation("Order {OrderNumber} was cancelled before its stock arrived; releasing it", order.OrderNumber);
            await publisher.PublishAsync(new ReleaseStock(order.Id.Value), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!order.CanConfirmStock)
        {
            logger.LogInformation("Duplicate StockReserved for {OrderNumber} ignored ({Status})", order.OrderNumber, order.Status);
            return;
        }

        order.ConfirmStock(clock.UtcNow);
        await publisher.PublishAsync(new AuthorizePayment(order.Id.Value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The warehouse cannot serve the order: cancel it, and make sure the card is never charged.</summary>
    public static async Task Handle(
        StockReservationRejected message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        IClock clock, ILogger<StockReservationRejected> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        if (!order.CanCancel)
        {
            return;
        }

        var shortages = message.Shortages.Count == 0
            ? null
            : string.Join(", ", message.Shortages.Select(static s => $"{s.Sku} {s.Available}/{s.Requested}"));
        order.Cancel(CancellationReasons.OutOfStock, shortages, clock.UtcNow);

        await publisher.PublishAsync(new VoidPayment(order.Id.Value), cancellationToken).ConfigureAwait(false);
        OrderingMetrics.Transitions.Add(1,
            new KeyValuePair<string, object?>("status", "cancelled"), new KeyValuePair<string, object?>("reason", CancellationReasons.OutOfStock));
        logger.LogInformation("Order {OrderNumber} cancelled: out of stock ({Shortages})", order.OrderNumber, shortages);
    }

    /// <summary>The request for the stock was given up: cancel the order, so that it does not wait for ever.</summary>
    public static async Task Handle(
        StockReservationAbandoned message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        IClock clock, ILogger<StockReservationAbandoned> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        if (!order.CanConfirmStock)
        {
            // Only an order that still waits for its stock is stopped by this. One that moved on did so
            // because the request was answered after all, by a replay from the error queue.
            logger.LogInformation("Order {OrderNumber} no longer waits for stock ({Status}); nothing to stop", order.OrderNumber, order.Status);
            return;
        }

        order.Cancel(CancellationReasons.ReservationFailed, null, clock.UtcNow);

        // The release voids the request, so a replay of it from the error queue holds nothing.
        await publisher.PublishAsync(new ReleaseStock(order.Id.Value), cancellationToken).ConfigureAwait(false);
        await publisher.PublishAsync(new VoidPayment(order.Id.Value), cancellationToken).ConfigureAwait(false);
        OrderingMetrics.Transitions.Add(1,
            new KeyValuePair<string, object?>("status", "cancelled"), new KeyValuePair<string, object?>("reason", CancellationReasons.ReservationFailed));
        logger.LogWarning("Order {OrderNumber} cancelled: its stock request was given up", order.OrderNumber);
    }

    /// <summary>The card was charged: the order is paid, unless it was cancelled meanwhile, in which case the money goes back.</summary>
    public static async Task Handle(
        PaymentAuthorized message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        IClock clock, ILogger<PaymentAuthorized> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        if (order.IsCancelled)
        {
            // The shopper cancelled while the provider was charging the card. Pay it back.
            logger.LogWarning("Order {OrderNumber} was charged after it was cancelled; refunding", order.OrderNumber);
            await publisher.PublishAsync(new RefundPayment(order.Id.Value), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!order.CanMarkPaid)
        {
            logger.LogInformation("Duplicate PaymentAuthorized for {OrderNumber} ignored ({Status})", order.OrderNumber, order.Status);
            return;
        }

        order.MarkPaid(message.ProviderReference, clock.UtcNow);
        OrderingMetrics.Transitions.Add(1, new KeyValuePair<string, object?>("status", "paid"));
    }

    /// <summary>The bank said no: cancel the order and give the stock back.</summary>
    public static async Task Handle(
        PaymentDeclined message, IOrderRepository orders, IMessagePublisher publisher, IUnitOfWork unitOfWork,
        IClock clock, ILogger<PaymentDeclined> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(publisher);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        if (!order.CanCancel)
        {
            return;
        }

        order.Cancel(CancellationReasons.PaymentDeclined, message.DeclineCode, clock.UtcNow);
        await publisher.PublishAsync(new ReleaseStock(order.Id.Value), cancellationToken).ConfigureAwait(false);
        OrderingMetrics.Transitions.Add(1,
            new KeyValuePair<string, object?>("status", "cancelled"), new KeyValuePair<string, object?>("reason", CancellationReasons.PaymentDeclined));
        logger.LogInformation("Order {OrderNumber} cancelled: payment declined ({DeclineCode})", order.OrderNumber, message.DeclineCode);
    }

    /// <summary>The money went back. Recorded once, however often it is reported.</summary>
    public static async Task Handle(
        PaymentRefunded message, IOrderRepository orders, IUnitOfWork unitOfWork, IClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var order = await Load(orders, message.OrderId, cancellationToken).ConfigureAwait(false);
        order.RecordRefund(message.RefundReference, clock.UtcNow);
    }

    private static async Task<Order> Load(IOrderRepository orders, Guid orderId, CancellationToken cancellationToken) =>
        await orders.GetAsync(new OrderId(orderId), cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"Order {orderId} does not exist; a module answered for an order this host never placed.");
}
