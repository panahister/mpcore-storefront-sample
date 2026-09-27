using Microsoft.EntityFrameworkCore;
using Storefront.Analytics.Application.Ports;
using Storefront.Analytics.Application.Views;
using Storefront.Analytics.Domain;

namespace Storefront.Analytics.Infrastructure.Persistence;

/// <summary>
/// The figures, asked of TimescaleDB in its own language: <c>time_bucket</c> groups a time series into
/// intervals, and a hypertable answers it from the chunks the period touches, not from the whole table.
/// </summary>
/// <remarks>
/// The SQL is written here, in the adapter, and nowhere else. Every value is a parameter; the statements
/// contain no input.
/// </remarks>
public sealed class SalesReadModel(AppDbContext database) : ISalesReadModel
{
    public async Task<IReadOnlyList<HourlySales>> HourlyAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await database.Database.SqlQuery<HourlySales>($"""
            SELECT time_bucket(INTERVAL '1 hour', occurred_on_utc) AS "Hour",
                   currency AS "Currency",
                   count(*) AS "PaidOrders",
                   coalesce(sum(amount), 0) AS "Revenue"
            FROM analytics.order_facts
            WHERE kind = 'Paid' AND occurred_on_utc >= {from} AND occurred_on_utc < {to}
            GROUP BY 1, 2
            ORDER BY 1, 2
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RegionalSales>> ByRegionAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await database.Database.SqlQuery<RegionalSales>($"""
            SELECT coalesce(region, '') AS "Region",
                   coalesce(city, '') AS "City",
                   currency AS "Currency",
                   count(*) AS "PlacedOrders",
                   coalesce(sum(item_count), 0)::bigint AS "Items",
                   coalesce(sum(amount), 0) AS "Value"
            FROM analytics.order_facts
            WHERE kind = 'Placed' AND occurred_on_utc >= {from} AND occurred_on_utc < {to}
            GROUP BY 1, 2, 3
            ORDER BY "Value" DESC, 1, 2
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

    // A reason that is null, empty or only blanks is missing, and is counted under UNKNOWN (A6).
    public async Task<IReadOnlyList<CancellationsByReason>> CancellationsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await database.Database.SqlQuery<CancellationsByReason>($"""
            SELECT coalesce(nullif(btrim(reason), ''), 'UNKNOWN') AS "Reason",
                   count(*) AS "CancelledOrders"
            FROM analytics.order_facts
            WHERE kind = 'Cancelled' AND occurred_on_utc >= {from} AND occurred_on_utc < {to}
            GROUP BY 1
            ORDER BY "CancelledOrders" DESC, 1
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
}
