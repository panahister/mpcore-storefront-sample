using System.Diagnostics.Metrics;
using Storefront.Commerce.Modules.Catalog.Application.Ports;
using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Catalog.Domain.Events;
using Microsoft.Extensions.Logging;
using MPCore.Application.Time;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Modules.Catalog.Application.Events;

/// <summary>Tells purchasing, once per crossing (rule C9): a restock alert, a metric and a warning in the log.</summary>
public static class StockFellBelowThresholdHandler
{
    private static readonly Meter Meter = new("Storefront.Commerce");
    private static readonly Counter<long> AlertsRaised = Meter.CreateCounter<long>(
        "storefront.catalog.restock_alerts", unit: "{alert}", description: "Restock alerts raised for purchasing.");

    public static void Handle(
        StockFellBelowThreshold @event,
        IRestockAlertRepository alerts,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<StockFellBelowThreshold> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(alerts);

        alerts.Add(RestockAlert.Raise(@event.Sku, @event.ProductName, @event.Available, @event.Threshold, clock.UtcNow));
        AlertsRaised.Add(1, new KeyValuePair<string, object?>("sku", @event.Sku));
        logger.LogWarning(
            "Restock alert: {Sku} ({ProductName}) has {Available} available, threshold {Threshold}",
            @event.Sku, @event.ProductName, @event.Available, @event.Threshold);
    }
}
