Implemented the Analytics cancellations report without committing.

Key behavior:

- `GET /v1/analytics/sales/cancellations?from=&to=`
- Protected by the existing `analyst` policy.
- Defaults to the last seven days and enforces A2.
- Uses the existing five-second, whole-minute cache behavior.
- Counts `Cancelled` facts by reason.
- Maps a database `NULL` reason to `UNKNOWN`.
- Orders by `cancelledOrders` descending, then `reason`.
- Added handler tests for defaults, response rows, and caching.
- Documented A6 and the endpoint.

Changed files:

- [SalesEndpoints.cs](../../../analytics/src/Storefront.Analytics.Api/Rest/Endpoints/SalesEndpoints.cs)
- [ISalesReadModel.cs](../../../analytics/src/Storefront.Analytics.Application/Ports/ISalesReadModel.cs)
- [SalesReports.cs](../../../analytics/src/Storefront.Analytics.Application/Queries/SalesReports.cs)
- [SalesViews.cs](../../../analytics/src/Storefront.Analytics.Application/Views/SalesViews.cs)
- [SalesReadModel.cs](../../../analytics/src/Storefront.Analytics.Infrastructure/Persistence/SalesReadModel.cs)
- [AnalyticsTests.cs](../../../analytics/tests/Storefront.Analytics.Tests/AnalyticsTests.cs)
- [business.md](../../../docs/business.md)

Verification:

- Explicit serial build succeeded: **0 warnings, 0 errors**.
- The requested exact command, `dotnet test analytics/Storefront.Analytics.Backend.sln`, failed before compilation because the sandbox denied MSBuild’s named-pipe socket: `MSB1025` / `SocketException (13): Permission denied`.
- Retrying serially with `-m:1` built successfully, but VSTest was then aborted before test discovery because the sandbox also denied its localhost `TcpListener`. Consequently, no tests executed and no legitimate pass count is available.
- `git diff --check` passed.
- Only the seven authorized files above are modified.

The only interpretation worth noting is that “missing reason” means SQL `NULL`; empty non-null reason strings remain unchanged.
