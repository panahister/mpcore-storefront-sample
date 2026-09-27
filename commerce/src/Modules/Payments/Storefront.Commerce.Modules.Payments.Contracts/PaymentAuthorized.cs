namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>The provider approved the charge.</summary>
public sealed record PaymentAuthorized(Guid OrderId, string ProviderReference);
