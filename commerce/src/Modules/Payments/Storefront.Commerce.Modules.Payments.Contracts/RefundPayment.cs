namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>Pay a charged and cancelled order back. Idempotent.</summary>
public sealed record RefundPayment(Guid OrderId);
