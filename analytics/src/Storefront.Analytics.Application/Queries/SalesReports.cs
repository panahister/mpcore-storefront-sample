using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Application.Time;
using MPCore.Caching.Abstractions;
using Storefront.Analytics.Application.Ports;
using Storefront.Analytics.Application.Views;

namespace Storefront.Analytics.Application.Queries;

/// <summary>Paid orders and revenue per hour. Without a period: the last twenty-four hours.</summary>
public sealed record GetHourlySales(DateTimeOffset? From, DateTimeOffset? To) : IQuery<Result<SalesReport<HourlySales>>>;

/// <summary>Orders placed per region and city. Without a period: the last seven days.</summary>
public sealed record GetSalesByRegion(DateTimeOffset? From, DateTimeOffset? To) : IQuery<Result<SalesReport<RegionalSales>>>;

/// <summary>
/// A query only reads: it declares no unit of work, publishes nothing, and reads through a read-model port
/// that returns views. It is the only thing an HTTP <c>GET</c> sends (RFC 9110 requires <c>GET</c> to be safe).
/// </summary>
/// <remarks>
/// A report is an aggregate over many rows, and a dashboard asks for the same one again and again. It is
/// kept in this process for a few seconds (the cache-aside pattern, read-through). Nothing evicts it: the
/// figures are a few seconds behind the orders anyway, so a few seconds more is the honest price. A period
/// is counted in whole minutes, so that two requests a moment apart ask for the same report.
/// </remarks>
public static class SalesReportsHandler
{
    public static readonly TimeSpan ReportLifetime = TimeSpan.FromSeconds(5);

    public static string CacheKey(string report, DateTimeOffset from, DateTimeOffset to) =>
        $"analytics:sales:{report}:{from.UtcDateTime:yyyyMMddHHmm}:{to.UtcDateTime:yyyyMMddHHmm}";

    public static async Task<Result<SalesReport<HourlySales>>> Handle(
        GetHourlySales query, ISalesReadModel sales, IReadThroughCache cache, IClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(cache);
        var period = Period(query.From, query.To, TimeSpan.FromHours(24), clock.UtcNow);
        if (period.IsFailure)
        {
            return Result<SalesReport<HourlySales>>.FromFailure(period.FailureDescriptor!);
        }

        var (from, to) = period.Value;
        var rows = await cache.GetOrCreateAsync(
            CacheKey("hourly", from, to),
            async ct => await sales.HourlyAsync(from, to, ct).ConfigureAwait(false),
            ReportLifetime,
            cancellationToken).ConfigureAwait(false);
        return Result<SalesReport<HourlySales>>.Success(new SalesReport<HourlySales>(from, to, rows));
    }

    public static async Task<Result<SalesReport<RegionalSales>>> Handle(
        GetSalesByRegion query, ISalesReadModel sales, IReadThroughCache cache, IClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(cache);
        var period = Period(query.From, query.To, TimeSpan.FromDays(7), clock.UtcNow);
        if (period.IsFailure)
        {
            return Result<SalesReport<RegionalSales>>.FromFailure(period.FailureDescriptor!);
        }

        var (from, to) = period.Value;
        var rows = await cache.GetOrCreateAsync(
            CacheKey("by-region", from, to),
            async ct => await sales.ByRegionAsync(from, to, ct).ConfigureAwait(false),
            ReportLifetime,
            cancellationToken).ConfigureAwait(false);
        return Result<SalesReport<RegionalSales>>.Success(new SalesReport<RegionalSales>(from, to, rows));
    }

    /// <summary>
    /// The period a report covers, in whole minutes: what was asked for, or the default that ends with the
    /// minute that has begun.
    /// </summary>
    public static Result<(DateTimeOffset From, DateTimeOffset To)> Period(
        DateTimeOffset? from, DateTimeOffset? to, TimeSpan defaultLength, DateTimeOffset now)
    {
        var end = to is null ? WholeMinute(now).AddMinutes(1) : WholeMinute(to.Value);
        var start = from is null ? end - defaultLength : WholeMinute(from.Value);
        if (start >= end)
        {
            return Result<(DateTimeOffset, DateTimeOffset)>.FromFailure(AnalyticsFailures.PeriodInvalid());
        }

        return end - start > AnalyticsFailures.MaximumPeriod
            ? Result<(DateTimeOffset, DateTimeOffset)>.FromFailure(AnalyticsFailures.PeriodTooLong())
            : Result<(DateTimeOffset, DateTimeOffset)>.Success((start, end));
    }

    private static DateTimeOffset WholeMinute(DateTimeOffset moment)
    {
        var utc = moment.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }
}
