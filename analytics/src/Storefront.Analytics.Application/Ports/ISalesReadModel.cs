using Storefront.Analytics.Application.Views;

namespace Storefront.Analytics.Application.Ports;

/// <summary>The read side: figures out, never a fact.</summary>
public interface ISalesReadModel
{
    /// <summary>Paid orders and their revenue, per hour and currency, oldest first.</summary>
    Task<IReadOnlyList<HourlySales>> HourlyAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    /// <summary>Orders placed and their value, per region, city and currency, largest first.</summary>
    Task<IReadOnlyList<RegionalSales>> ByRegionAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
