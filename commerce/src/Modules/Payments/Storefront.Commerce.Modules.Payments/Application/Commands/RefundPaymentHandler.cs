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

/// <summary>Pays back a charged order that was cancelled. Idempotent: a refunded payment repeats its answer.</summary>
public static class RefundPaymentHandler
{
    public static async Task Handle(
        RefundPayment command,
        IPaymentRepository payments,
        IPaymentGateway gateway,
        IMessagePublisher publisher,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<RefundPayment> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(publisher);

        var payment = await payments.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return;
        }

        if (payment.Status == PaymentStatus.Refunded)
        {
            await publisher.PublishAsync(new PaymentRefunded(payment.OrderId, payment.RefundReference!), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (payment.Status != PaymentStatus.Authorized)
        {
            logger.LogInformation("Refund for order {OrderId} skipped: payment is {Status}", payment.OrderId, payment.Status);
            return;
        }

        var result = await gateway.RefundAsync(
            $"refund-{payment.OrderId}", payment.ProviderReference!, payment.Amount, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != GatewayOutcome.Approved)
        {
            throw new ResultFailureException(PaymentFailures.ProviderUnavailable());
        }

        payment.MarkRefunded(result.Reference!, clock.UtcNow);
        await publisher.PublishAsync(new PaymentRefunded(payment.OrderId, result.Reference!), cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync("payments", "payment-refunded", nameof(Payment), payment.OrderId.ToString(),
            new Dictionary<string, string>
            {
                ["amount"] = payment.Amount.ToString(CultureInfo.InvariantCulture),
                ["refund_reference"] = result.Reference!
            }, cancellationToken).ConfigureAwait(false);
    }
}
