namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>The money went back.</summary>
public sealed record PaymentRefunded(Guid OrderId, string RefundReference);
