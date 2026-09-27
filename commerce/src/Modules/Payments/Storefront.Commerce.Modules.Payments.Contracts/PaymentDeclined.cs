namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>The provider refused the charge. A business answer, not a fault.</summary>
public sealed record PaymentDeclined(Guid OrderId, string DeclineCode);
