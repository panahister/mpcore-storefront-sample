namespace Storefront.Commerce.Modules.Ordering.Application.Views;

public sealed record OrderSummary(
    Guid OrderId, string OrderNumber, string Status, decimal Total, string Currency, int ItemCount, DateTimeOffset PlacedOnUtc);
