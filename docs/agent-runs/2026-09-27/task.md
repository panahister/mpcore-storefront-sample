Use the skill storefront-implement-vertical-slice.

The owner has approved one capability, in the Analytics backend only:

  An analyst reads how many orders were cancelled, per reason.

Acceptance criteria, all approved:
1. GET /v1/analytics/sales/cancellations?from=&to= , for the role "analyst", like the two reports that exist.
2. It answers a report like the other two: "from", "to", and "rows". A row has "reason" and "cancelledOrders". Rows are ordered by cancelledOrders, largest first, then by reason.
3. It counts the order facts of the kind Cancelled whose moment is in the period.
4. The period follows the rules the other reports follow (A2 in docs/business.md). Without a period it covers the last 7 days.
5. It is kept in the cache the way the other reports are (A4).
6. A cancellation whose reason is missing is counted under the reason "UNKNOWN".

What to deliver:
- the query, its handler and its view in the Application layer, the read model's new method with its SQL, and the endpoint;
- unit tests for the handler, in the style of the tests that exist;
- rule A6 in docs/business.md, section 10, and the endpoint in the table under it.

Change nothing outside the analytics folder and docs/business.md. Do not commit. Build the Analytics solution and run its tests with: dotnet test analytics/Storefront.Analytics.Backend.sln . Report the real output of the tests, the files you changed, and anything you were unsure about.
