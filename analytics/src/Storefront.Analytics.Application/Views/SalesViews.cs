namespace Storefront.Analytics.Application.Views;

/// <summary>One hour of paid orders.</summary>
public sealed record HourlySales(DateTimeOffset Hour, string Currency, long PaidOrders, decimal Revenue);

/// <summary>The orders placed for one city.</summary>
public sealed record RegionalSales(string Region, string City, string Currency, long PlacedOrders, long Items, decimal Value);

/// <summary>A report and the period it covers.</summary>
public sealed record SalesReport<TRow>(DateTimeOffset From, DateTimeOffset To, IReadOnlyList<TRow> Rows);
