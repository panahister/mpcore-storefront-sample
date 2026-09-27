using MPCore.Application.Messaging;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using Storefront.Fulfillment.Application.Ports;
using Storefront.Fulfillment.Application.Views;
using Storefront.Fulfillment.Domain;

namespace Storefront.Fulfillment.Application.Queries;

/// <summary>The warehouse's work list, oldest first. <paramref name="Status"/> is <c>Pending</c>, <c>Dispatched</c> or empty for both.</summary>
public sealed record ListShipments(string? Status, int Page = 1, int Size = PageRequest.DefaultSize) : IQuery<Result<Page<ShipmentSummary>>>;

public static class ListShipmentsHandler
{
    public static async Task<Result<Page<ShipmentSummary>>> Handle(ListShipments query, IShipmentReadModel shipments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        ShipmentStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ShipmentStatus>(query.Status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Result<Page<ShipmentSummary>>.FromFailure(FulfillmentFailures.StatusUnknown());
            }

            status = parsed;
        }

        return Result<Page<ShipmentSummary>>.Success(
            await shipments.ListAsync(status, new PageRequest(query.Page, query.Size), cancellationToken).ConfigureAwait(false));
    }
}
