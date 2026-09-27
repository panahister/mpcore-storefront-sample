using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Contracts;
using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Payments.Application.Commands;

/// <summary>Registers the charge of a new order with the token of its payment intent, and answers Ordering. Runs from a durable local queue.</summary>
/// <remarks>
/// <para>
/// Idempotent through the <see cref="Payment"/>, whose identity is the order's: a redelivered request finds
/// the payment and repeats the answer. Hohpe and Woolf's <i>Idempotent Receiver</i>.
/// </para>
/// <para>
/// The checkout asked whether the intent could be used, but another checkout may have used it since. So it
/// is decided again here, where it is used: an intent that cannot pay for this order declines the payment
/// at once, and the order process ends the order as it does for a declined card.
/// </para>
/// </remarks>
public static class RegisterPaymentHandler
{
    public const string IntentUnusable = "PAYMENT_INTENT_UNUSABLE";

    public static async Task Handle(
        RegisterPayment command,
        IPaymentRepository payments,
        IPaymentIntentRepository intents,
        IMessagePublisher publisher,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<RegisterPayment> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(publisher);

        var payment = await payments.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            var now = clock.UtcNow;
            var intent = await intents.GetAsync(command.PaymentIntentId, cancellationToken).ConfigureAwait(false);
            if (intent is not null && intent.IsUsableBy(command.BuyerId, now))
            {
                var token = intent.UseFor(command.OrderId, command.BuyerId, now);
                payment = Payment.Register(command.OrderId, command.Amount, command.Currency, token, now);
            }
            else
            {
                logger.LogWarning("Payment intent {PaymentIntentId} cannot pay for order {OrderId}; the payment is refused",
                    command.PaymentIntentId, command.OrderId);
                payment = Payment.Refuse(command.OrderId, command.Amount, command.Currency, IntentUnusable, now);
            }

            payments.Add(payment);
        }

        await publisher.PublishAsync(
            payment.Status == PaymentStatus.Declined
                ? new PaymentDeclined(payment.OrderId, payment.DeclineCode!)
                : new PaymentRegistered(payment.OrderId),
            cancellationToken).ConfigureAwait(false);
    }
}
