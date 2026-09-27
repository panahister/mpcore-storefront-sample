using System.Diagnostics.Metrics;

namespace Storefront.Commerce.Modules.Payments.Application;

public static class PaymentMetrics
{
    private static readonly Meter Meter = new("Storefront.Commerce");

    public static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "storefront.payments.outcomes", unit: "{payment}", description: "Payment provider answers, by outcome.");
}
