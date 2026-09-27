using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using Storefront.Fulfillment.Api.Hosting;
using Storefront.Fulfillment.Application;
using Storefront.Fulfillment.Application.Commands;
using Storefront.Fulfillment.Application.Queries;
using Storefront.Fulfillment.Application.Views;
using Wolverine;
using ResultFailureException = MPCore.Application.Results.ResultFailureException;

namespace Storefront.Fulfillment.Api.Grpc.Services;

/// <summary>The warehouse's work list and the dispatch of a parcel.</summary>
/// <remarks>
/// Every method does the same three things: read the request, invoke one command or query through
/// Wolverine, and map the view. A failure is thrown as <see cref="ResultFailureException"/>, and MP Core's
/// gRPC failure handling turns it into the native status with rich error details.
/// </remarks>
[Authorize(Policy = FulfillmentPolicies.Warehouse)]
public sealed class ShipmentsService(IMessageBus bus) : Shipments.ShipmentsBase
{
    /// <inheritdoc />
    public override async Task<ListShipmentsResponse> ListShipments(ListShipmentsRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var page = (await bus.InvokeAsync<Result<Page<ShipmentSummary>>>(
            new ListShipments(
                string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
                request.Page <= 0 ? 1 : request.Page,
                request.Size <= 0 ? PageRequest.DefaultSize : request.Size),
            context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();

        var response = new ListShipmentsResponse { Total = page.Total, Page = page.Number, Size = page.Size };
        response.Shipments.AddRange(page.Items.Select(static s => new ShipmentSummaryMessage
        {
            OrderId = s.OrderId.ToString(),
            OrderNumber = s.OrderNumber,
            Status = s.Status,
            City = s.City,
            ItemCount = s.ItemCount,
            ReceivedOn = Timestamp.FromDateTimeOffset(s.ReceivedOnUtc)
        }));
        return response;
    }

    /// <inheritdoc />
    public override async Task<ShipmentReply> GetShipment(GetShipmentRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return ToReply((await bus.InvokeAsync<Result<ShipmentView>>(new GetShipment(ParseOrderId(request.OrderId)), context.CancellationToken)
            .ConfigureAwait(false)).ValueOrThrow());
    }

    /// <inheritdoc />
    public override async Task<ShipmentReply> DispatchShipment(DispatchShipmentRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return ToReply((await bus.InvokeAsync<Result<ShipmentView>>(
            new DispatchShipment(ParseOrderId(request.OrderId), request.Carrier, request.TrackingCode), context.CancellationToken)
            .ConfigureAwait(false)).ValueOrThrow());
    }

    private static Guid ParseOrderId(string value) =>
        Guid.TryParse(value, out var id)
            ? id
            : throw new ResultFailureException(FulfillmentFailures.OrderIdInvalid());

    private static ShipmentReply ToReply(ShipmentView shipment)
    {
        var reply = new ShipmentReply
        {
            OrderId = shipment.OrderId.ToString(),
            OrderNumber = shipment.OrderNumber,
            Status = shipment.Status,
            ShipTo = new DeliveryAddressMessage
            {
                RecipientName = shipment.ShipTo.RecipientName,
                Phone = shipment.ShipTo.Phone,
                Province = shipment.ShipTo.Province,
                City = shipment.ShipTo.City,
                Line = shipment.ShipTo.Line,
                PostalCode = shipment.ShipTo.PostalCode
            },
            Carrier = shipment.Carrier ?? string.Empty,
            TrackingCode = shipment.TrackingCode ?? string.Empty,
            ReceivedOn = Timestamp.FromDateTimeOffset(shipment.ReceivedOnUtc)
        };
        if (shipment.DispatchedOnUtc is { } dispatched)
        {
            reply.DispatchedOn = Timestamp.FromDateTimeOffset(dispatched);
        }

        reply.Lines.AddRange(shipment.Lines.Select(static l => new ShipmentLineMessage { Sku = l.Sku, ProductName = l.ProductName, Quantity = l.Quantity }));
        return reply;
    }
}
