namespace Storefront.Commerce.Modules.Payments.Domain.Rules;

/// <summary>Rule P8: a payment intent pays for one order, of the shopper who created it, before it expires.</summary>
public sealed class PaymentIntentMustBeUsable(PaymentIntent intent, string buyerId, DateTimeOffset now) : PaymentsRule(
    "PAYMENT_INTENT_UNUSABLE", "payments.payment_intent_unusable")
{
    public override bool IsBroken() => !intent.IsUsableBy(buyerId, now);
}
