namespace Storefront.Commerce.Modules.Payments.Domain.Rules;

/// <summary>Rule P1: a card is charged, declined or voided only while the payment is pending. Nothing is charged twice.</summary>
public sealed class PaymentMustBePending(Payment payment) : PaymentsRule(
    "PAYMENT_NOT_PENDING", "payments.payment_not_pending",
    new Dictionary<string, string> { ["status"] = payment.Status.ToString() })
{
    public override bool IsBroken() => payment.Status != PaymentStatus.Pending;
}
