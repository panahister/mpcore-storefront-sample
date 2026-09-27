using Storefront.Commerce.Modules.Payments.Domain.Rules;
using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Payments.Domain;

/// <summary>
/// A shopper's payment token, handed to Payments before checkout and kept here until an order uses it.
/// Checkout carries the intent's identity; the token itself never leaves this module.
/// </summary>
/// <remarks>
/// <para>
/// The provider's token is a secret (rule P5). Carried in a checkout message, it would sit in the queue
/// tables for as long as the messaging framework keeps a handled message, and in the error queue for as
/// long as an operator leaves it there. The payment intent keeps it where it belongs:
/// in one row of the Payments module, until an order consumes it, at the latest until the intent expires.
/// </para>
/// <para>
/// The pattern is the payment intent of the card providers' own APIs (Stripe's <c>PaymentIntent</c>, for
/// example): the browser creates it with the provider's token, and the order refers to it by identity.
/// It is also what has to exist on the day Payments becomes a service of its own.
/// </para>
/// </remarks>
public sealed class PaymentIntent : AggregateRoot<Guid>
{
    /// <summary>How long an unused intent may be used. Afterwards its token is erased.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private PaymentIntent()
    {
        BuyerId = string.Empty;
    }

    private PaymentIntent(Guid id, string buyerId, string paymentToken, DateTimeOffset now)
        : base(id)
    {
        BuyerId = buyerId;
        PaymentToken = paymentToken;
        ExpiresOnUtc = now + Lifetime;
    }

    /// <summary>The shopper it belongs to; only this shopper's order may use it.</summary>
    public string BuyerId { get; private set; }

    /// <summary>The provider's token; null once an order has taken it or the intent has expired.</summary>
    public string? PaymentToken { get; private set; }

    public DateTimeOffset ExpiresOnUtc { get; private set; }

    /// <summary>The order that used it; null while it is unused.</summary>
    public Guid? UsedByOrderId { get; private set; }

    public DateTimeOffset? UsedOnUtc { get; private set; }

    public static PaymentIntent Create(string buyerId, string paymentToken, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buyerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentToken);
        return new PaymentIntent(Guid.CreateVersion7(now), buyerId, paymentToken, now);
    }

    /// <summary>Answers without throwing, for the checkout that asks before it commits and for the handler that must decide.</summary>
    public bool IsUsableBy(string buyerId, DateTimeOffset now) =>
        UsedByOrderId is null && PaymentToken is not null && now < ExpiresOnUtc
        && string.Equals(BuyerId, buyerId, StringComparison.Ordinal);

    /// <summary>
    /// Hands the token to the order's payment and forgets it. Rule P8: an intent pays for one order, of its
    /// own shopper, before it expires.
    /// </summary>
    public string UseFor(Guid orderId, string buyerId, DateTimeOffset now)
    {
        CheckRule(new PaymentIntentMustBeUsable(this, buyerId, now));

        var token = PaymentToken!;
        PaymentToken = null;
        UsedByOrderId = orderId;
        UsedOnUtc = now;
        return token;
    }
}
