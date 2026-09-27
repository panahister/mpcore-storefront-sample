using Storefront.Commerce.Modules.Basket.Application.Ports;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Storefront.Commerce.Modules.Basket.Contracts;
using Storefront.Commerce.Modules.Payments.Contracts;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;
using MPCore.Security;

namespace Storefront.Commerce.Modules.Basket.Application.Commands;

/// <summary>Checkout. <paramref name="ExpectedTotal"/> is what the shopper saw; a basket that costs something else is refused.</summary>
/// <param name="PaymentIntentId">The payment intent the shopper created with Payments (<c>POST /v1/payments/intents</c>).</param>
/// <remarks>
/// Wolverine logs a message whose handling failed by printing it, and a record prints every property. The
/// address is personal data, so this record says what it prints.
/// </remarks>
public sealed record Checkout(CheckoutAddress ShippingAddress, Guid PaymentIntentId, decimal ExpectedTotal) : ICommand<Result<CheckoutAccepted>>
{
    public override string ToString() => $"{nameof(Checkout)} {{ ShippingAddress = {ShippingAddress}, ExpectedTotal = {ExpectedTotal} }}";
}

/// <summary>
/// Checkout: the basket empties and says so. That is the whole transaction, and it changes one module.
/// </summary>
/// <remarks>
/// <para>
/// <b>A module writes only its own data; other modules learn through messages.</b> The Basket does not
/// create the order, register the charge or ask for stock. It publishes <see cref="BasketCheckedOut"/> with
/// a snapshot of what it held, in the transaction that empties it (the transactional outbox), and Ordering
/// reacts. Vaughn Vernon's aggregate rule (<i>Implementing Domain-Driven Design</i>) is one aggregate per
/// transaction and eventual consistency between them; Kamil Grzybek's <i>Modular Monolith with DDD</i> lets
/// modules talk through events only; Microsoft's eShop checks out the same way. The day a module becomes a
/// service, the message changes its transport and nothing else.
/// </para>
/// <para>
/// <b>The price is eventual consistency.</b> The shopper is told "accepted" with the order's identity
/// (HTTP 202), not shown an order: that exists a moment later. What is checked here is what only the Basket
/// can know, the total; what only the warehouse or the bank can know arrives afterwards, as it always did.
/// </para>
/// <para>
/// <b>The total check shows MP Core's rollback policy at work.</b> The total that is compared is the total
/// of what was taken, the very snapshot that would be published, and taking it has already emptied the
/// tracked basket. Returning the failure then would commit an empty basket and no checkout. It does not:
/// MP Core's <c>ResultFailureRollbackPolicy</c> sees a failed result on top of pending changes and turns it
/// into a <c>ResultFailureException</c>, the transaction rolls back, and the caller receives exactly the
/// failure below. The basket is untouched.
/// </para>
/// </remarks>
public static class CheckoutHandler
{
    public static async Task<Result<CheckoutAccepted>> Handle(
        Checkout command,
        ICurrentActorAccessor actor,
        IBasketRepository baskets,
        IPaymentIntentLookup paymentIntents,
        IMessagePublisher publisher,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(paymentIntents);

        // Identity comes from the validated token, never from the request.
        var buyer = actor.Current;
        if (string.IsNullOrWhiteSpace(buyer.SubjectId))
        {
            return Result<CheckoutAccepted>.FromFailure(BasketFailures.BuyerRequired());
        }

        var basket = await baskets.GetAsync(buyer.SubjectId, cancellationToken).ConfigureAwait(false);
        if (basket is null || basket.Lines.Count == 0)
        {
            return Result<CheckoutAccepted>.FromFailure(BasketFailures.BasketEmpty());
        }

        // A read across the boundary, through Payments' Contracts, while the shopper can still be told. It
        // cannot promise: another checkout may use the intent first, and Payments decides again then.
        if (!await paymentIntents.IsUsableAsync(command.PaymentIntentId, buyer.SubjectId, cancellationToken).ConfigureAwait(false))
        {
            return Result<CheckoutAccepted>.FromFailure(BasketFailures.PaymentIntentUnusable());
        }

        var taken = basket.TakeForCheckout();
        if (taken.Total != command.ExpectedTotal)
        {
            // The basket is already emptied in the change tracker. See the remarks: MP Core rolls this back.
            return Result<CheckoutAccepted>.FromFailure(BasketFailures.TotalChanged());
        }

        // A version 7 UUID is ordered by time and needs no coordination, so the Basket can name the order
        // before Ordering has heard of it.
        var now = clock.UtcNow;
        var orderId = Guid.CreateVersion7(now);
        await publisher.PublishAsync(
            new BasketCheckedOut(
                orderId, buyer.SubjectId, buyer.UserName ?? buyer.SubjectId, command.ShippingAddress,
                [.. taken.Lines.Select(static l => new CheckoutLine(l.Sku, l.ProductName, l.UnitPrice, l.Quantity))],
                taken.Total, taken.Currency, command.PaymentIntentId, now),
            cancellationToken).ConfigureAwait(false);

        return Result<CheckoutAccepted>.Success(new CheckoutAccepted(orderId, taken.Total, taken.Currency, now));
    }
}
