using Storefront.Commerce.Modules.Ordering.Domain;

namespace Storefront.Commerce.Modules.Ordering.Application.Views;

public static class OrderViews
{
    public static OrderView Of(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var a = order.ShippingAddress;
        return new OrderView(
            order.Id.Value, order.OrderNumber, order.Status.ToString(), order.BuyerName,
            [.. order.Lines.Select(static l => new OrderLineView(l.Sku, l.ProductName, l.UnitPrice, l.Quantity, l.LineTotal))],
            order.Total, order.Currency,
            new ShippingAddressView(a.RecipientName, a.Phone.Value, a.Province, a.City, a.Line, a.PostalCode.Value),
            order.PaymentReference, order.CancellationReason, order.Carrier, order.TrackingCode, order.PlacedOnUtc,
            [.. order.History.Select(static h => new OrderHistoryView(h.Status, h.OccurredOnUtc, h.Note))]);
    }
}
