using System.Globalization;
using Storefront.Commerce.Api.Hosting;
using Storefront.Commerce.Modules.Catalog.Application.Queries;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Ordering.Application;
using Storefront.Commerce.Modules.Ordering.Application.Commands;
using Storefront.Commerce.Modules.Ordering.Application.Queries;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using Wolverine;
using ResultFailureException = MPCore.Application.Results.ResultFailureException;

namespace Storefront.Commerce.Api.Grpc.Services;

/// <summary>
/// The warehouse's view of orders.
/// </summary>
/// <remarks>
/// Same commands and queries as REST, a different transport. A failure is thrown as
/// <see cref="ResultFailureException"/> and MP Core's gRPC failure handling turns it into the native
/// status with rich error details (<c>google.rpc.ErrorInfo</c> and friends), so a gRPC client receives
/// the same stable failure identity a REST client receives in Problem Details.
/// </remarks>
[Authorize(Policy = StorefrontPolicies.Warehouse)]
public sealed class FulfillmentService(IMessageBus bus) : Fulfillment.FulfillmentBase
{
    /// <inheritdoc />
    public override async Task<ListOrdersToShipResponse> ListOrdersToShip(ListOrdersToShipRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var page = (await bus.InvokeAsync<Result<Page<OrderSummary>>>(
            new ListOrders("Paid", request.Page <= 0 ? 1 : request.Page, request.Size <= 0 ? PageRequest.DefaultSize : request.Size, "placed", false),
            context.CancellationToken).ConfigureAwait(false)).ValueOrThrow();

        var response = new ListOrdersToShipResponse { Total = page.Total, Page = page.Number, Size = page.Size };
        response.Orders.AddRange(page.Items.Select(static o => new OrderSummaryMessage
        {
            OrderId = o.OrderId.ToString(),
            OrderNumber = o.OrderNumber,
            Total = o.Total.ToString(CultureInfo.InvariantCulture),
            Currency = o.Currency,
            ItemCount = o.ItemCount,
            PlacedOn = Timestamp.FromDateTimeOffset(o.PlacedOnUtc)
        }));
        return response;
    }

    /// <inheritdoc />
    public override async Task<OrderReply> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var order = (await bus.InvokeAsync<Result<OrderView>>(new GetOrder(ParseOrderId(request.OrderId)), context.CancellationToken)
            .ConfigureAwait(false)).ValueOrThrow();
        return ToReply(order);
    }

    /// <inheritdoc />
    public override async Task<OrderReply> ShipOrder(ShipOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var order = (await bus.InvokeAsync<Result<OrderView>>(
            new ShipOrder(ParseOrderId(request.OrderId), request.Carrier, request.TrackingCode), context.CancellationToken)
            .ConfigureAwait(false)).ValueOrThrow();
        return ToReply(order);
    }

    private static Guid ParseOrderId(string value) =>
        Guid.TryParse(value, out var id)
            ? id
            : throw new ResultFailureException(OrderingFailures.OrderIdInvalid());

    private static OrderReply ToReply(OrderView order)
    {
        var reply = new OrderReply
        {
            OrderId = order.OrderId.ToString(),
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            Total = order.Total.ToString(CultureInfo.InvariantCulture),
            Currency = order.Currency,
            Carrier = order.Carrier ?? string.Empty,
            TrackingCode = order.TrackingCode ?? string.Empty,
            ShipTo = new ShippingAddressMessage
            {
                RecipientName = order.ShippingAddress.RecipientName,
                Phone = order.ShippingAddress.Phone,
                Province = order.ShippingAddress.Province,
                City = order.ShippingAddress.City,
                Line = order.ShippingAddress.Line,
                PostalCode = order.ShippingAddress.PostalCode
            }
        };
        reply.Lines.AddRange(order.Lines.Select(static l => new OrderLineMessage
        {
            Sku = l.Sku,
            ProductName = l.ProductName,
            UnitPrice = l.UnitPrice.ToString(CultureInfo.InvariantCulture),
            Quantity = l.Quantity
        }));
        return reply;
    }
}

/// <summary>Product lookups for other Storefront services. Any authenticated caller.</summary>
public sealed class CatalogService(IMessageBus bus) : Catalog.CatalogBase
{
    /// <inheritdoc />
    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var product = (await bus.InvokeAsync<Result<ProductDetails>>(new GetProductDetails(request.Sku), context.CancellationToken)
            .ConfigureAwait(false)).ValueOrThrow();
        return new ProductReply
        {
            Sku = product.Sku,
            Name = product.Name,
            Price = product.Price.ToString(CultureInfo.InvariantCulture),
            Currency = product.Currency,
            Available = product.Available,
            Status = product.Status,
            PriceVersion = product.PriceVersion
        };
    }
}
