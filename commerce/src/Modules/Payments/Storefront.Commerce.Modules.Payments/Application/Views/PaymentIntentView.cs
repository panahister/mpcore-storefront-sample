namespace Storefront.Commerce.Modules.Payments.Application.Views;

/// <summary>What the storefront needs to check out with: the intent's identity, and until when it can be used.</summary>
public sealed record PaymentIntentView(Guid PaymentIntentId, DateTimeOffset ExpiresOnUtc);
