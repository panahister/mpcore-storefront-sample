using Storefront.Commerce.Modules.Payments.Domain.Rules;
using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Payments.Domain;

/// <summary>
/// The charge for one order. <b>One payment per order, keyed by the order.</b> The order identifier is also
/// the idempotency key the provider receives, so a retried charge is the same charge.
/// </summary>
/// <remarks>
/// The payment token is a secret the provider issued to the shopper's browser. It is kept only until the
/// provider answers, then erased (rule P5): an authorized or declined payment holds no token.
/// </remarks>
public sealed class Payment : AggregateRoot<Guid>
{
    private Payment()
    {
        Currency = string.Empty;
    }

    private Payment(Guid orderId, decimal amount, string currency, string? paymentToken, DateTimeOffset now)
        : base(orderId)
    {
        Amount = amount;
        Currency = currency;
        PaymentToken = paymentToken;
        Status = PaymentStatus.Pending;
        RegisteredOnUtc = now;
    }

    public Guid OrderId => Id;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public string? PaymentToken { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? DeclineCode { get; private set; }

    public string? RefundReference { get; private set; }

    public DateTimeOffset RegisteredOnUtc { get; private set; }

    public DateTimeOffset? SettledOnUtc { get; private set; }

    public bool IsPending => Status == PaymentStatus.Pending;

    public static Payment Register(Guid orderId, decimal amount, string currency, string paymentToken, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentToken);
        CheckRule(new AmountMustBePositive(amount));
        return new Payment(orderId, amount, currency, paymentToken, now);
    }

    /// <summary>
    /// The charge of an order that cannot be collected, because its payment intent cannot be used. Nothing
    /// was sent to the provider; the payment is declined at once, so the order process ends the order.
    /// </summary>
    public static Payment Refuse(Guid orderId, decimal amount, string currency, string reasonCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        CheckRule(new AmountMustBePositive(amount));
        return new Payment(orderId, amount, currency, null, now)
        {
            Status = PaymentStatus.Declined,
            DeclineCode = reasonCode,
            SettledOnUtc = now
        };
    }

    public void MarkAuthorized(string providerReference, DateTimeOffset now)
    {
        CheckRule(new PaymentMustBePending(this));

        Status = PaymentStatus.Authorized;
        ProviderReference = providerReference;
        PaymentToken = null;
        SettledOnUtc = now;
    }

    public void MarkDeclined(string declineCode, DateTimeOffset now)
    {
        CheckRule(new PaymentMustBePending(this));

        Status = PaymentStatus.Declined;
        DeclineCode = declineCode;
        PaymentToken = null;
        SettledOnUtc = now;
    }

    public void Void(DateTimeOffset now)
    {
        CheckRule(new PaymentMustBePending(this));

        Status = PaymentStatus.Voided;
        PaymentToken = null;
        SettledOnUtc = now;
    }

    public void MarkRefunded(string refundReference, DateTimeOffset now)
    {
        CheckRule(new PaymentMustBeAuthorized(this));

        Status = PaymentStatus.Refunded;
        RefundReference = refundReference;
        SettledOnUtc = now;
    }
}
