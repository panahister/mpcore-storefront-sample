using System.Globalization;
using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Contracts;
using Storefront.Commerce.Modules.Payments.Domain;
using Microsoft.Extensions.Logging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Audit;
using MPCore.Messaging.Abstractions;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Payments.Application.Commands;

/// <summary>Charges the order. Runs once stock is confirmed.</summary>
/// <remarks>
/// <para>
/// Idempotent three ways: a payment that already has an answer repeats it; the provider receives the
/// order identifier as its idempotency key; and the resilient HTTP client's own retries are therefore
/// safe.
/// </para>
/// <para>
/// When the provider cannot be reached even after the client's retries, the handler throws a
/// retryable <see cref="ResultFailureException"/>. The host's error policy obeys its retry directive:
/// redelivery with a cooldown, then the error queue. The order stays "awaiting payment" meanwhile,
/// which is the truth.
/// </para>
/// </remarks>
public static class AuthorizePaymentHandler
{
    public static async Task Handle(
        AuthorizePayment command,
        IPaymentRepository payments,
        IPaymentGateway gateway,
        IMessagePublisher publisher,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AuthorizePayment> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(publisher);

        var payment = await payments.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No payment was registered for order {command.OrderId}.");

        // A repeated request repeats the answer; a payment that will never be charged says so.
        switch (payment.Status)
        {
            case PaymentStatus.Authorized:
                await publisher.PublishAsync(new PaymentAuthorized(payment.OrderId, payment.ProviderReference!), cancellationToken).ConfigureAwait(false);
                return;
            case PaymentStatus.Declined:
                await publisher.PublishAsync(new PaymentDeclined(payment.OrderId, payment.DeclineCode!), cancellationToken).ConfigureAwait(false);
                return;
            case PaymentStatus.Voided or PaymentStatus.Refunded:
                logger.LogInformation("Payment for order {OrderId} is {Status}; not charging", payment.OrderId, payment.Status);
                return;
        }

        var result = await gateway.AuthorizeAsync(
            payment.OrderId.ToString(), payment.Amount, payment.Currency, payment.PaymentToken!, cancellationToken).ConfigureAwait(false);
        PaymentMetrics.Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", result.Outcome.ToString().ToLowerInvariant()));

        var now = clock.UtcNow;
        switch (result.Outcome)
        {
            case GatewayOutcome.Approved:
                payment.MarkAuthorized(result.Reference!, now);
                await publisher.PublishAsync(new PaymentAuthorized(payment.OrderId, result.Reference!), cancellationToken).ConfigureAwait(false);
                await audit.RecordAsync("payments", "payment-authorized", nameof(Payment), payment.OrderId.ToString(),
                    new Dictionary<string, string>
                    {
                        ["amount"] = payment.Amount.ToString(CultureInfo.InvariantCulture),
                        ["provider_reference"] = result.Reference!
                    }, cancellationToken).ConfigureAwait(false);
                break;
            case GatewayOutcome.Declined:
                payment.MarkDeclined(result.Reference ?? "DECLINED", now);
                await publisher.PublishAsync(new PaymentDeclined(payment.OrderId, payment.DeclineCode!), cancellationToken).ConfigureAwait(false);
                await audit.RecordAsync("payments", "payment-declined", nameof(Payment), payment.OrderId.ToString(),
                    new Dictionary<string, string> { ["decline_code"] = payment.DeclineCode! }, cancellationToken).ConfigureAwait(false);
                break;
            default:
                logger.LogWarning("Payment provider unavailable for order {OrderId}; the message will be retried", payment.OrderId);
                throw new ResultFailureException(PaymentFailures.ProviderUnavailable());
        }

        logger.LogInformation("Payment for order {OrderId}: {Outcome}", payment.OrderId, result.Outcome);
    }
}
