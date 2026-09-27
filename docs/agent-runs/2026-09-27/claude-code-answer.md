The cancellations report is built and the Analytics tests pass (19 of 19), but the SQL has never run against a database. The handler tests replace the read model with a fake, and I wasn't allowed to run `docker ps`, so I couldn't check whether TimescaleDB was up. That means criteria 2 (the ordering), 3 and 6 (the `UNKNOWN` rule) rest on the SQL as written, not on anything I saw work. Nothing is committed.

## Test output
`dotnet test analytics/Storefront.Analytics.Backend.sln` built the solution and ended with:
```
Passed!  - Failed:     0, Passed:    19, Skipped:     0, Total:    19, Duration: 329 ms - Storefront.Analytics.Tests.dll (net10.0)
```
12 tests existed before and I added 7. They passed on their first run; I never saw them fail against broken code. `git status` was not allowed either, so the file list below comes from my own edits.

## Files changed
- `analytics/src/Storefront.Analytics.Application/Queries/SalesReports.cs`: the query `GetCancellationsByReason` and a third `Handle` on `SalesReportsHandler`. It uses the shared `Period` check (A2), defaults to 7 days, and caches under the key `cancellations` for 5 seconds (A4).
- `analytics/src/Storefront.Analytics.Application/Views/SalesViews.cs`: the row view `CancellationsByReason(string Reason, long CancelledOrders)`. The response has `from`, `to` and `rows`, with each row as `reason` and `cancelledOrders`.
- `analytics/src/Storefront.Analytics.Application/Ports/ISalesReadModel.cs`: the new method `CancellationsAsync`.
- `analytics/src/Storefront.Analytics.Infrastructure/Persistence/SalesReadModel.cs`: its SQL. It counts facts of kind `'Cancelled'` in the period, groups by `coalesce(nullif(btrim(reason), ''), 'UNKNOWN')`, and orders by `"CancelledOrders" DESC, 1`.
- `analytics/src/Storefront.Analytics.Api/Rest/Endpoints/SalesEndpoints.cs`: `GET /v1/analytics/sales/cancellations`. It sits in the existing group, so it requires the `analyst` role.
- `analytics/tests/Storefront.Analytics.Tests/AnalyticsTests.cs`: the test fake now implements the new method, and a new `CancellationReportTests` class covers:
  - the 7-day default;
  - rows passed through unchanged;
  - the cache (one read, and the key);
  - whole minutes;
  - the three refused periods.
- `docs/business.md`, section 10: rule A6, the endpoint row, and in the file tree "the two reports" became "the three reports".

## What I was unsure about
1. **"Missing" reason.** I count null, empty and blank-only reasons as `UNKNOWN`. The criterion could be read as null only. Also, a reason that is literally `UNKNOWN` lands in the same row.
2. **Where the `UNKNOWN` rule lives.** It is in the SQL, next to A5, so no unit test covers it. To test it without a database I would move it into the handler, which then has to merge and re-sort rows.
3. **Ordering "by reason".** Like the existing reports, the SQL sorts without naming a collation, so ties order by the database's collation, not byte order.
4. **One file per use case.** MP Core's skill asks for a file per query. I followed this repository's habit instead and added the query to `SalesReports.cs`, beside the other two reports, to share `Period` and `CacheKey`.
5. **Rule A3** still lists the defaults of the first two reports only. I put the 7-day default in A6 and left A3 alone.

To check the SQL, the rest of criterion 1 (the role) and the JSON shape, run the backend with `scripts/up.sh` and `scripts/run.sh analytics` and call the endpoint as an analyst. No scenario covers it yet.
