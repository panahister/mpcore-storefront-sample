namespace Storefront.Commerce.Modules.Ordering.Application.Views;

/// <summary>The whole order as REST and gRPC show it. A view: the aggregate never leaves the module.</summary>
public sealed record OrderView(
    Guid OrderId, string OrderNumber, string Status, string BuyerName, IReadOnlyList<OrderLineView> Lines,
    decimal Total, string Currency, ShippingAddressView ShippingAddress, string? PaymentReference,
    string? CancellationReason, string? Carrier, string? TrackingCode, DateTimeOffset PlacedOnUtc,
    IReadOnlyList<OrderHistoryView> History);
