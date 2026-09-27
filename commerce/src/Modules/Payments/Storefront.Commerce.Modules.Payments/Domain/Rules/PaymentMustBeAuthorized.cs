namespace Storefront.Commerce.Modules.Payments.Domain.Rules;

/// <summary>Rule P7: only money that was taken can be paid back.</summary>
public sealed class PaymentMustBeAuthorized(Payment payment) : PaymentsRule(
    "PAYMENT_NOT_AUTHORIZED", "payments.payment_not_authorized",
    new Dictionary<string, string> { ["status"] = payment.Status.ToString() })
{
    public override bool IsBroken() => payment.Status != PaymentStatus.Authorized;
}
