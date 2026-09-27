namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>One step of the order's life, as the shopper and support see it.</summary>
public sealed record OrderHistoryEntry(string Status, DateTimeOffset OccurredOnUtc, string? Note);
