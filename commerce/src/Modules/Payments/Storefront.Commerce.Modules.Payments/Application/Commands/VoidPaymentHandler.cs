using Storefront.Commerce.Modules.Payments.Application.Ports;
using Storefront.Commerce.Modules.Payments.Contracts;
using MPCore.Application.Time;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Payments.Application.Commands;

/// <summary>Never charges an order that was cancelled first. Idempotent: a payment that is no longer pending is left alone.</summary>
public static class VoidPaymentHandler
{
    public static async Task Handle(
        VoidPayment command, IPaymentRepository payments, IUnitOfWork unitOfWork, IClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await payments.GetAsync(command.OrderId, cancellationToken).ConfigureAwait(false);
        if (payment is { IsPending: true })
        {
            payment.Void(clock.UtcNow);
        }
    }
}
