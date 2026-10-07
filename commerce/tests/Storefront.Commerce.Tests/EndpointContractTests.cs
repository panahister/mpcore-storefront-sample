using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MPCore.Application.Querying;
using Wolverine;
using MPCore.Audit;
using MPCore.Localization;
using Storefront.Commerce.Api.Hosting;
using Storefront.Commerce.Api.Rest.Endpoints;
using Storefront.Commerce.Modules.Ordering.Application.Views;
using Storefront.Commerce.Modules.Basket.Application.Views;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using Storefront.Commerce.Modules.Payments.Application.Views;

namespace Storefront.Commerce.Tests;

public sealed class EndpointContractTests
{
    [Theory]
    [InlineData("BrowseProducts", typeof(Page<ProductSummary>), 200)]
    [InlineData("GetProduct", typeof(ProductDetails), 200)]
    [InlineData("ListProduct", typeof(ProductStockView), 201)]
    [InlineData("GetMyBasket", typeof(BasketView), 200)]
    [InlineData("Checkout", typeof(CheckoutAccepted), 202)]
    [InlineData("CreatePaymentIntent", typeof(PaymentIntentView), 201)]
    [InlineData("ListMyOrders", typeof(Page<OrderSummary>), 200)]
    [InlineData("GetOrder", typeof(OrderView), 200)]
    [InlineData("CancelOrder", typeof(OrderView), 200)]
    [InlineData("ChangeProductPrice", typeof(ProductStockView), 200)]
    [InlineData("RestockProduct", typeof(ProductStockView), 200)]
    [InlineData("DiscontinueProduct", typeof(ProductStockView), 200)]
    [InlineData("GetProductStock", typeof(ProductStockView), 200)]
    [InlineData("ListRestockAlerts", typeof(Page<RestockAlertView>), 200)]
    [InlineData("AcknowledgeBasketPrices", typeof(BasketView), 200)]
    [InlineData("SetBasketItem", typeof(BasketView), 200)]
    [InlineData("RemoveBasketItem", typeof(BasketView), 200)]
    [InlineData("ListOrders", typeof(Page<OrderSummary>), 200)]
    [InlineData("QueryAuditTrail", typeof(AuditPage), 200)]
    [InlineData("ListTranslations", typeof(IReadOnlyList<MessageTranslationEntry>), 200)]
    [InlineData("SetTranslation", typeof(TranslationView), 200)]
    [InlineData("RemoveTranslation", typeof(void), 204)]
    public async Task Real_endpoint_metadata_describes_its_existing_success_body_and_status(string operation, Type body, int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IMessageBus>(_ => throw new InvalidOperationException("Contract tests never invoke business operations."));
        builder.Services.AddSingleton<MPCore.Application.Idempotency.IIdempotentExecutor>(_ => throw new InvalidOperationException("Contract tests never invoke business operations."));
        builder.Services.AddSingleton<MPCore.Audit.IAuditQuery>(_ => throw new InvalidOperationException("Contract tests never invoke business operations."));
        await using var app = builder.Build();
        app.MapStorefrontEndpoints();
        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints),
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == operation);
        var produces = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>();
        Assert.Contains(produces, metadata => metadata.StatusCode == status && metadata.Type == body &&
            (body == typeof(void) ? !metadata.ContentTypes.Any() : metadata.ContentTypes.Contains("application/json")));
    }
}
