namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>Never charge this order: it was cancelled before the charge. Idempotent.</summary>
public sealed record VoidPayment(Guid OrderId);
