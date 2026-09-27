using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Application.Views;

public sealed record ShipmentLineView(string Sku, string ProductName, int Quantity);

public sealed record DeliveryAddressView(string RecipientName, string Phone, string Province, string City, string Line, string PostalCode);

public sealed record ShipmentView(
    Guid OrderId, string OrderNumber, string Status, DeliveryAddressView ShipTo, IReadOnlyList<ShipmentLineView> Lines,
    string? Carrier, string? TrackingCode, DateTimeOffset ReceivedOnUtc, DateTimeOffset? DispatchedOnUtc);

public sealed record ShipmentSummary(Guid OrderId, string OrderNumber, string Status, string City, int ItemCount, DateTimeOffset ReceivedOnUtc);

public static class ShipmentViews
{
    public static ShipmentView Of(Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        var a = shipment.Address;
        return new ShipmentView(
            shipment.OrderId, shipment.OrderNumber, shipment.Status.ToString(),
            new DeliveryAddressView(a.RecipientName, a.Phone, a.Province, a.City, a.Line, a.PostalCode),
            [.. shipment.Lines.Select(static l => new ShipmentLineView(l.Sku, l.ProductName, l.Quantity))],
            shipment.Carrier, shipment.TrackingCode, shipment.ReceivedOnUtc, shipment.DispatchedOnUtc);
    }
}
