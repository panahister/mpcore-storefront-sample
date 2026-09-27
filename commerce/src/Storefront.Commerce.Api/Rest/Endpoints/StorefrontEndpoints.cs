using Storefront.Commerce.Api.Hosting;
using Storefront.Commerce.Modules.Basket.Application.Commands;
using Storefront.Commerce.Modules.Basket.Application.Queries;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Application.Queries;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Ordering.Application.Commands;
using Storefront.Commerce.Modules.Ordering.Application.Queries;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Payments.Application.Commands;
using Storefront.Commerce.Modules.Payments.Application.Views;
using MPCore.Application.Idempotency;
using MPCore.Application.Querying;
using MPCore.Application.Results;
using MPCore.Audit;
using MPCore.Localization;
using MPCore.Transport.Http;
using Wolverine;

namespace Storefront.Commerce.Api.Rest.Endpoints;

/// <summary>
/// The REST surface of the storefront and the back office.
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint does the same three things: bind the request, invoke one command or query through
/// Wolverine, and render the <see cref="Result"/> with MP Core's <c>ToHttpResult</c>. A failure becomes
/// RFC 9457 Problem Details with the failure's stable code; no endpoint builds an error by hand.
/// </para>
/// <para>
/// Two endpoints send their command through MP Core's <see cref="IIdempotentExecutor"/> (ADR-013), because a
/// repeat of either would cost somebody money or stock. Checkout requires an <c>Idempotency-Key</c>: a
/// shopper whose connection dropped retries with the same key and receives the first answer, marked
/// <c>Idempotency-Replayed: true</c>. Restocking accepts one; it is protected by its delivery note either way.
/// The header is the IETF HTTPAPI working group's draft, made common by Stripe.
/// </para>
/// <para>
/// Authorization is declared per group. The only anonymous endpoints are the storefront's catalog
/// reads — a deliberate product decision, made visibly here, against MP Core's protect-by-default
/// fallback. Everything else needs a token from the <c>storefront</c> realm.
/// </para>
/// </remarks>
public static class StorefrontEndpoints
{
    /// <summary>Maps every storefront and back-office endpoint, and returns them for listener binding.</summary>
    public static IReadOnlyList<RouteGroupBuilder> MapStorefrontEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return [MapCatalog(endpoints), MapBasket(endpoints), MapPayments(endpoints), MapOrders(endpoints), MapBackOffice(endpoints)];
    }

    private static RouteGroupBuilder MapCatalog(IEndpointRouteBuilder endpoints)
    {
        var catalog = endpoints.MapGroup("/v1/catalog").WithTags("Catalog");

        catalog.MapGet("/products", static async (
                string? category, string? search, int? page, int? size, string? sort, bool? desc, bool? includeDiscontinued,
                IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<Page<ProductSummary>>>(
                    new BrowseProducts(category, search, page ?? 1, size ?? PageRequest.DefaultSize, sort, desc ?? false, includeDiscontinued ?? false),
                    ct).ConfigureAwait(false)).ToHttpResult(Results.Ok))
            .AllowAnonymous()
            .WithName("BrowseProducts");

        catalog.MapGet("/products/{sku}", static async (string sku, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<ProductDetails>>(new GetProductDetails(sku), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .AllowAnonymous()
            .WithName("GetProduct");

        var manage = catalog.MapGroup("").RequireAuthorization(StorefrontPolicies.CatalogManager);

        manage.MapPost("/products", static async (ListProduct request, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<ProductStockView>>(request, ct).ConfigureAwait(false))
                .ToHttpResult(view => Results.Created($"/v1/catalog/products/{view.Sku}", view)))
            .WithName("ListProduct");

        manage.MapPut("/products/{sku}/price", static async (string sku, ChangePriceRequest request, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<ProductStockView>>(new ChangeProductPrice(sku, request.NewPrice, request.Reason), ct)
                    .ConfigureAwait(false)).ToHttpResult(Results.Ok))
            .WithName("ChangeProductPrice");

        manage.MapPost("/products/{sku}/restock", static async (
                string sku, RestockRequest request, IIdempotentExecutor idempotent, IMessageBus bus, CancellationToken ct) =>
            {
                var command = new RestockProduct(sku, request.Quantity, request.Reference);
                return (await idempotent.ExecuteAsync(command, token => bus.InvokeAsync<Result<ProductStockView>>(command, token), ct)
                    .ConfigureAwait(false)).ToHttpResult(Results.Ok);
            })
            .WithName("RestockProduct");

        manage.MapPost("/products/{sku}/discontinue", static async (string sku, ReasonRequest request, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<ProductStockView>>(new DiscontinueProduct(sku, request.Reason), ct)
                    .ConfigureAwait(false)).ToHttpResult(Results.Ok))
            .WithName("DiscontinueProduct");

        manage.MapGet("/products/{sku}/stock", static async (string sku, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<ProductStockView>>(new GetProductStock(sku), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("GetProductStock");

        manage.MapGet("/restock-alerts", static async (int? page, int? size, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<Page<RestockAlertView>>>(
                    new ListRestockAlerts(page ?? 1, size ?? PageRequest.DefaultSize), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("ListRestockAlerts");

        return catalog;
    }

    private static RouteGroupBuilder MapBasket(IEndpointRouteBuilder endpoints)
    {
        var basket = endpoints.MapGroup("/v1/basket").WithTags("Basket").RequireAuthorization(StorefrontPolicies.Customer);

        basket.MapGet("/", static async (IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<BasketView>>(new GetMyBasket(), ct).ConfigureAwait(false)).ToHttpResult(Results.Ok))
            .WithName("GetMyBasket");

        // Reading the basket changes nothing; saying "I have seen the new prices" is a command of its own.
        basket.MapPost("/acknowledge-prices", static async (IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<BasketView>>(new AcknowledgeBasketPrices(), ct).ConfigureAwait(false)).ToHttpResult(Results.Ok))
            .WithName("AcknowledgeBasketPrices");

        basket.MapPut("/items/{sku}", static async (string sku, QuantityRequest request, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<BasketView>>(new SetBasketItem(sku, request.Quantity), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("SetBasketItem");

        basket.MapDelete("/items/{sku}", static async (string sku, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<BasketView>>(new SetBasketItem(sku, 0), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("RemoveBasketItem");

        // 202, not 201: the basket has been taken and the order has its identity, but Ordering creates it a
        // moment later, from the message this command published. The Location is where it will be.
        basket.MapPost("/checkout", static async (Checkout request, IIdempotentExecutor idempotent, IMessageBus bus, CancellationToken ct) =>
                (await idempotent.ExecuteAsync(request, token => bus.InvokeAsync<Result<CheckoutAccepted>>(request, token), ct)
                    .ConfigureAwait(false)).ToHttpResult(accepted => Results.Accepted($"/v1/orders/{accepted.OrderId}", accepted)))
            .RequireIdempotencyKey()
            .WithName("Checkout");

        return basket;
    }

    private static RouteGroupBuilder MapPayments(IEndpointRouteBuilder endpoints)
    {
        var payments = endpoints.MapGroup("/v1/payments").WithTags("Payments").RequireAuthorization(StorefrontPolicies.Customer);

        // The shopper's browser hands over the provider's token here, before checkout, and checks out with
        // the reference it gets back. The token never travels further (rule P5). A repeat
        // without a key creates a second intent, which expires unused; with a key, it is answered once.
        payments.MapPost("/intents", static async (CreatePaymentIntent request, IIdempotentExecutor idempotent, IMessageBus bus, CancellationToken ct) =>
                (await idempotent.ExecuteAsync(request, token => bus.InvokeAsync<Result<PaymentIntentView>>(request, token), ct)
                    .ConfigureAwait(false)).ToHttpResult(intent => Results.Created((string?)null, intent)))
            .WithName("CreatePaymentIntent");

        return payments;
    }

    private static RouteGroupBuilder MapOrders(IEndpointRouteBuilder endpoints)
    {
        var orders = endpoints.MapGroup("/v1/orders").WithTags("Orders");

        orders.MapGet("/", static async (int? page, int? size, string? sort, bool? desc, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<Page<OrderSummary>>>(
                    new ListMyOrders(page ?? 1, size ?? PageRequest.DefaultSize, sort, desc ?? true), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .RequireAuthorization(StorefrontPolicies.Customer)
            .WithName("ListMyOrders");

        orders.MapGet("/{orderId:guid}", static async (Guid orderId, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<OrderView>>(new GetOrder(orderId), ct).ConfigureAwait(false)).ToHttpResult(Results.Ok))
            .RequireAuthorization(StorefrontPolicies.OrderReaders)
            .WithName("GetOrder");

        orders.MapPost("/{orderId:guid}/cancel", static async (Guid orderId, CancelRequest? request, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<OrderView>>(new CancelOrder(orderId, request?.Note), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .RequireAuthorization(StorefrontPolicies.OrderCancellers)
            .WithName("CancelOrder");

        return orders;
    }

    private static RouteGroupBuilder MapBackOffice(IEndpointRouteBuilder endpoints)
    {
        var office = endpoints.MapGroup("/v1/backoffice").WithTags("BackOffice").RequireAuthorization(StorefrontPolicies.Support);

        office.MapGet("/orders", static async (
                string? status, int? page, int? size, string? sort, bool? desc, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<Page<OrderSummary>>>(
                    new ListOrders(status, page ?? 1, size ?? PageRequest.DefaultSize, sort, desc ?? true), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("ListOrders");

        // The audit trail is read straight through MP Core's IAuditQuery port: a read-only view of what
        // the framework itself recorded, with values already masked by the capture policy.
        office.MapGet("/audit", static async (
                string? module, string? entityType, string? entityId, int? page, int? size, IAuditQuery audit, CancellationToken ct) =>
                Results.Ok(await audit.QueryAsync(
                    new AuditQueryFilter { Module = module, EntityType = entityType, EntityId = entityId },
                    new AuditPageRequest(Math.Max(page ?? 1, 1), Math.Clamp(size ?? 50, 1, 200)), ct).ConfigureAwait(false)))
            .WithName("QueryAuditTrail");

        // Translations of the failure messages, edited at run time (ADR-012 §5). A key must be one the code
        // uses; the change is visible on every instance within the refresher's interval.
        office.MapGet("/translations", static async (string? culture, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<IReadOnlyList<MessageTranslationEntry>>>(new ListTranslations(culture), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("ListTranslations");

        office.MapPut("/translations/{culture}/{key}", static async (string culture, string key, TranslationRequest request, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result<TranslationView>>(new SetTranslation(key, culture, request.Text), ct).ConfigureAwait(false))
                .ToHttpResult(Results.Ok))
            .WithName("SetTranslation");

        office.MapDelete("/translations/{culture}/{key}", static async (string culture, string key, IMessageBus bus, CancellationToken ct) =>
                (await bus.InvokeAsync<Result>(new RemoveTranslation(key, culture), ct).ConfigureAwait(false))
                .ToHttpResult())
            .WithName("RemoveTranslation");

        return office;
    }
}

/// <summary>A price change.</summary>
/// <param name="NewPrice">The new price of one unit, in dollars and cents.</param>
/// <param name="Reason">Why.</param>
public sealed record ChangePriceRequest(decimal NewPrice, string Reason);

/// <summary>A delivery into the warehouse.</summary>
/// <param name="Quantity">Units received.</param>
/// <param name="Reference">Delivery note or purchase order.</param>
public sealed record RestockRequest(int Quantity, string Reference);

/// <summary>A reason.</summary>
/// <param name="Reason">Why.</param>
public sealed record ReasonRequest(string Reason);

/// <summary>A quantity.</summary>
/// <param name="Quantity">How many.</param>
public sealed record QuantityRequest(int Quantity);

/// <summary>A cancellation.</summary>
/// <param name="Note">Why. Required from support.</param>
public sealed record CancelRequest(string? Note);

public sealed record TranslationRequest(string Text);
