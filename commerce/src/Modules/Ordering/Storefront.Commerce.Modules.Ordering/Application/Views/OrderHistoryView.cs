namespace Storefront.Commerce.Modules.Ordering.Application.Views;

public sealed record OrderHistoryView(string Status, DateTimeOffset OccurredOnUtc, string? Note);
