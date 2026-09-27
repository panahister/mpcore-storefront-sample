using MPCore.Application.Results;
using MPCore.Transport.Http;
using Storefront.Analytics.Api.Hosting;
using Storefront.Analytics.Application.Queries;
using Storefront.Analytics.Application.Views;
using Wolverine;

namespace Storefront.Analytics.Api.Rest.Endpoints;

/// <summary>
/// The figures. Every endpoint is a <c>GET</c> and sends a query: nothing here changes anything.
/// </summary>
public static class SalesEndpoints
{
    public static RouteGroupBuilder MapSalesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var sales = endpoints.MapGroup("/v1/analytics/sales").WithTags("Sales").RequireAuthorization(AnalyticsPolicies.Analyst);

        sales.MapGet("/hourly", static async (DateTimeOffset? from, DateTimeOffset? to, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<SalesReport<HourlySales>>>(new GetHourlySales(from, to), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("GetHourlySales");

        sales.MapGet("/by-region", static async (DateTimeOffset? from, DateTimeOffset? to, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<SalesReport<RegionalSales>>>(new GetSalesByRegion(from, to), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("GetSalesByRegion");

        return sales;
    }
}
