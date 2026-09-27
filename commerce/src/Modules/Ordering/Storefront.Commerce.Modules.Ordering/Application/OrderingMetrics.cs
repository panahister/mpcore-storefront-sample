using System.Diagnostics.Metrics;

namespace Storefront.Commerce.Modules.Ordering.Application;

public static class OrderingMetrics
{
    private static readonly Meter Meter = new("Storefront.Commerce");

    public static readonly Counter<long> Placed = Meter.CreateCounter<long>(
        "storefront.ordering.orders_placed", unit: "{order}", description: "Orders placed at checkout.");

    public static readonly Counter<double> PlacedValue = Meter.CreateCounter<double>(
        "storefront.ordering.order_value", unit: "USD", description: "Value of orders placed.");

    public static readonly Counter<long> Transitions = Meter.CreateCounter<long>(
        "storefront.ordering.transitions", unit: "{order}", description: "Orders reaching Paid, Shipped or Cancelled.");
}
