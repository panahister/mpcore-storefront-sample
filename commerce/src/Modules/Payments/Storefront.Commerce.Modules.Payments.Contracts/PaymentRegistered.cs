namespace Storefront.Commerce.Modules.Payments.Contracts;

/// <summary>The charge of the order is registered and waits for the word to collect it.</summary>
public sealed record PaymentRegistered(Guid OrderId);
