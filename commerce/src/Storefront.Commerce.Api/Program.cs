using Storefront.Commerce.Api.Hosting;
using Storefront.Commerce.Api.Resources;
using Storefront.Commerce.Infrastructure;
using Storefront.Commerce.Infrastructure.Persistence;
using MPCore.Hosting;
using MPCore.Localization;
using MPCore.Localization.EntityFrameworkCore;
using MPCore.Messaging.Wolverine;
using MPCore.Observability;
using MPCore.Observability.Prometheus;
using MPCore.Security.AspNetCore;
using MPCore.Validation.FluentValidation;
using Storefront.Commerce.Api.Grpc.Services;
using MPCore.Transport.Grpc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Storefront.Commerce.Api.Rest.Endpoints;
using MPCore.Transport.Http;
using MPCore.Messaging.Wolverine.Kafka;
using MPCore.Messaging.Wolverine.RabbitMQ;
using System.Text.Json.Serialization;
using Storefront.Commerce.Modules.Basket.Resources;
using Storefront.Commerce.Modules.Catalog.Domain.Events;
using Storefront.Commerce.Modules.Catalog.Resources;
using Storefront.Commerce.Modules.Ordering.Domain.Events;
using Storefront.Commerce.Modules.Ordering.Resources;
using Storefront.Commerce.Modules.Payments.Resources;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wolverine.ErrorHandling;
using Wolverine.Kafka;
using Wolverine.RabbitMQ;
using ResultFailureException = MPCore.Application.Results.ResultFailureException;

var builder = WebApplication.CreateBuilder(args);

// Per-endpoint Kestrel "Protocols" values in appsettings.json are authoritative; no protocol is
// forced globally.
// A single cleartext Http1AndHttp2 endpoint cannot serve prior-knowledge h2c HTTP/2, so the
// endpoint that carries binary RPC declares Http2 exclusively.
const TransportMode HostTransport = TransportMode.Both;
TransportEndpointGuard.Validate(builder.Configuration, HostTransport);

builder.Services.AddMPCoreFoundation(new MPCoreObservabilityOptions
{
    ServiceName = "Storefront.Commerce",
    ServiceNamespace = "Storefront",
    ServiceVersion = typeof(Program).Assembly.GetName().Version?.ToString(),
    EnableOtlpExporter = builder.Configuration.GetValue("Observability:EnableOtlpExporter", false),
    // Each signal has its own destination, sampling and redaction settings; see docs/architecture.md.
    Signals = builder.Configuration.GetSection("Observability").Get<MPCoreObservabilitySignals>()
});
// Prometheus pull is a REST-listener surface. It carries no anonymous metadata: the scraper must
// present a bearer token, or the deployment must confine the listener to the scraper's network.
var metricsScrapeEnabled = builder.Configuration.GetValue("Observability:Metrics:Prometheus:Enabled", false);
if (metricsScrapeEnabled)
{
    builder.Services.AddMPCorePrometheusScrape();
}
// MP Core registers ASP.NET Core, HttpClient, runtime and Wolverine instrumentation. The product adds its
// own business meter (orders, payments, restock alerts) and Npgsql's spans, so a trace in Jaeger shows the
// SQL a request ran. MP Core has no extension point for this; the OpenTelemetry builder is additive.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter("Storefront.Commerce"))
    .WithTracing(tracing => tracing.AddSource("Npgsql"));
// Failures reach the caller as message keys. The catalog renders them in the language the caller asked
// for (Accept-Language): MP Core's own messages ship in English and Persian, and every module adds its
// resource file. Translations support edits at run time come from the database (see AddInfrastructure).
builder.Services.AddMPCoreMessageCatalog(catalog => catalog
    .AddResources<CatalogMessages>()
    .AddResources<BasketMessages>()
    .AddResources<OrderingMessages>()
    .AddResources<PaymentsMessages>()
    .AddResources<HostMessages>());

// Input validators (FluentValidation) of every handler assembly, this host included. They run before the
// handler; an invalid request is a 400 with one violation per field and never reaches business code.
foreach (var handlerAssembly in HandlerAssemblies.All.Append(typeof(Program).Assembly))
{
    builder.Services.AddMPCoreValidators(handlerAssembly);
}

// Bearer-only OIDC resource server. Login, signup, OTP, password and identity-provider
// administration are Product surfaces and are never implemented here.
builder.Services.AddMPCoreBearerAuthentication(options =>
{
    options.Authority = builder.Configuration["Security:Authority"]
        ?? throw new InvalidOperationException("Security:Authority is required.");
    options.RequireHttpsMetadata = builder.Configuration.GetValue("Security:RequireHttpsMetadata", true);
    foreach (var audience in builder.Configuration.GetSection("Security:Audiences").Get<string[]>() ?? [])
    {
        options.ValidAudiences.Add(audience);
    }
});
// Claim mapping follows the identity provider. Keycloak-shaped by default (realm and client roles,
// preferred_username, azp, service-account-* convention); GenericOidc reads a flat roles claim.
builder.Services.AddMPCoreCurrentActor(mapping =>
{
    if (string.Equals(builder.Configuration["Security:ClaimMapping:Preset"], "GenericOidc", StringComparison.OrdinalIgnoreCase))
    {
        mapping.UseGenericOidc();
    }
    else
    {
        mapping.UseKeycloakDefaults();
    }
});
// The tenant, when the token names one. Business code reads ITenantContext; audit records it.
builder.Services.AddMPCoreTenancyFromClaim(builder.Configuration["Security:TenantClaim"] ?? "tenant_id");
builder.Services.AddForwardedIdentityHeaderGuard();
// X-Forwarded-* is honoured only from the proxies listed here (APISIX or another gateway). Empty
// means the host reasons from the real connection and ignores the headers entirely.
builder.Services.AddMPCoreGatewayForwarding(options =>
{
    foreach (var proxy in builder.Configuration.GetSection("Gateway:TrustedProxies").Get<string[]>() ?? [])
    {
        options.TrustedProxies.Add(proxy);
    }
});

// MP Core does not map the health probes, so it cannot enforce anonymous access to them. This host
// maps them and therefore owns the decision, applied with AllowAnonymous() where they are mapped.
var allowAnonymousHealthEndpoints =
    builder.Configuration.GetValue("Security:AllowAnonymousHealthEndpoints", true);
builder.Services.AddMPCoreAuthorization();
// Storefront's own role policies, contributed to MP Core's default-deny authorization.
builder.Services.AddPolicyContributor<StorefrontPolicies>();
// Enums travel as their names ("Paid", "Rejected"), in requests and responses alike.
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Description surfaces are opt-in and default to Development only: an OpenAPI document and gRPC
// reflection describe the entire API to whoever can reach them.
var enableOpenApi = DeveloperEndpoints.IsDescriptionSurfaceEnabled(
    builder.Configuration, builder.Environment, "Transport:EnableOpenApi");
var enableGrpcReflection = DeveloperEndpoints.IsDescriptionSurfaceEnabled(
    builder.Configuration, builder.Environment, "Transport:EnableGrpcReflection");

// A description surface is only reachable without a token in Development. Enabling one explicitly in
// another environment keeps it behind the authenticated fallback: the flag says "expose it", not
// "expose it to anyone".
var anonymousDescriptionSurface = builder.Environment.IsDevelopment();

// What "alive" and "ready" mean is the same on every transport: Hosting/HostHealthChecks.cs.
builder.Services.AddHostHealthChecks();

builder.Services.AddGrpc().AddMPCoreFailureHandling(options => options.SupportedCultures.Add("fa"));
// The empty service name is the whole host; "live" asks the process only.
builder.Services.AddGrpcHealthChecks(options =>
    options.Services.Map(HostHealthChecks.Live, static check => check.Tags.Contains(HostHealthChecks.Live)));
if (enableGrpcReflection)
{
    builder.Services.AddGrpcReflection();
}
builder.Services.AddMPCoreHttpFailureHandling(options => options.SupportedCultures.Add("fa"));
builder.Services.AddMPCoreProblemDetailsSecurityResponses();
if (enableOpenApi)
{
    builder.Services.AddOpenApi();
}

var databaseConnection = builder.Configuration.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException("ConnectionStrings:PostgreSql is required.");
var cacheConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required for the selected cache.");
var paymentProviderAddress = builder.Configuration.GetValue<Uri>("PaymentProvider:BaseAddress")
    ?? throw new InvalidOperationException("PaymentProvider:BaseAddress is required.");
var paymentProviderMerchant = builder.Configuration["PaymentProvider:MerchantId"]
    ?? throw new InvalidOperationException("PaymentProvider:MerchantId is required.");
builder.Services.AddInfrastructure(databaseConnection, cacheConnection, paymentProviderAddress, paymentProviderMerchant);
builder.Services.Configure<MessageTranslationOptions>(builder.Configuration.GetSection("Localization:Translations"));
// AppDbContext is named here as the transaction owner, so a handler can depend on IUnitOfWork and
// still run inside the Entity Framework transaction whose commit releases its outgoing messages.
builder.Host.UseMPCoreWolverine<AppDbContext>(
    new WolverineFoundationOptions
    {
        ServiceName = "Storefront.Commerce",
        PersistenceConnectionString = databaseConnection,
        PersistenceSchemaName = "wolverine",
        // This project's own handlers (the Kafka consumer seam in Hosting/) are discovered from here.
        ApplicationAssembly = typeof(Program).Assembly
    },
    options =>
    {
        // Handlers are discovered only in the assemblies this host names. Nothing is scanned
        // implicitly and no catch-all policy exists; see Hosting/HandlerAssemblies.cs.
        options.DiscoverHandlersIn(HandlerAssemblies.All);
        options.UseMPCoreFluentValidation();
        // Kafka delivers at least once. An integration event this host has already processed stops at the
        // inbox, recorded by its EventId in the handler's own transaction (ADR-013).
        options.UseMPCoreInbox();

        // Integration events leave through Kafka, one topic per contract. Declared routes only: MP Core
        // drops an event with no route rather than guessing one (ADR-011 §5). Module messages
        // (ReserveStock, AuthorizePayment, ...) need no route: they go to the local handler that exists
        // for them, on Wolverine's durable local queues.
        options.PublishMessage<ProductPriceChanged>().ToKafkaTopic(CommerceTopics.ProductPriceChanged);
        options.PublishMessage<OrderPlaced>().ToKafkaTopic(CommerceTopics.OrderPlaced);
        options.PublishMessage<OrderPaid>().ToKafkaTopic(CommerceTopics.OrderPaid);
        options.PublishMessage<OrderCancelled>().ToKafkaTopic(CommerceTopics.OrderCancelled);
        options.PublishMessage<OrderShipped>().ToKafkaTopic(CommerceTopics.OrderShipped);

        // Work for one reader goes to a queue, not to the stream: the warehouse is told what to ship, and
        // tells this host when it has left. See Hosting/CommerceQueues.cs.
        options.PublishMessage<OrderReadyToShip>().ToRabbitQueue(CommerceQueues.OrdersReadyToShip).UseDurableOutbox();
        options.ListenToRabbitQueue(CommerceQueues.ShipmentsDispatched)
            .DefaultIncomingMessage<ShipmentDispatched>()
            .UseDurableInbox();

        // The Basket hears about price changes the way any other service would: from Kafka.
        options.ListenToKafkaTopic(CommerceTopics.ProductPriceChanged)
            .ProcessInline()
            .ConfigureConsumer(consumer => consumer.GroupId = CommerceTopics.BasketPricingGroup);

        // Kafka orders messages within a partition only. The key is the thing whose events must stay in
        // order: the order for order events, the SKU for price changes. MP Core's publisher port takes no
        // delivery options, so the key is derived here, from the message, by Wolverine's partitioning rules.
        options.MessagePartitioning.ByMessage<IOrderEvent>(e => e.OrderId.ToString());
        options.MessagePartitioning.ByMessage<ProductPriceChanged>(e => e.Sku);
        options.Policies.PropagateGroupIdToPartitionKey();

        // Failures carry a retry directive, and the broker obeys it: never retry what cannot succeed,
        // retry with a cooldown what might, and park the rest in the error queue for an operator.
        // Giving a message up is an answer too: whoever waits for it is told (Hosting/GivenUpMessages.cs).
        options.OnException<ResultFailureException>(e => !e.Failure.Retry.IsRetryable)
            .MoveToErrorQueue()
            .And(GivenUpMessages.TellTheProcessAsync, GivenUpMessages.Description);
        options.OnException<ResultFailureException>(e => e.Failure.Retry.IsRetryable)
            .RetryWithCooldown(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10))
            .Then.MoveToErrorQueue()
            .And(GivenUpMessages.TellTheProcessAsync, GivenUpMessages.Description);
        // Two orders racing for one product: the loser's save fails on the row version and is retried
        // against fresh numbers. A product on sale is one row that many orders want at once, so the pauses
        // grow, and each is lengthened by a random amount (full jitter: between the pause and twice the
        // pause), or the losers of one collision would all come back together and collide again. Marc
        // Brooker described the effect in "Exponential Backoff And Jitter" (AWS Architecture Blog, 2015).
        options.OnException<DbUpdateConcurrencyException>()
            .RetryWithCooldown(
                TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(800),
                TimeSpan.FromMilliseconds(1600), TimeSpan.FromMilliseconds(3200))
            .WithFullJitter()
            .Then.MoveToErrorQueue()
            .And(GivenUpMessages.TellTheProcessAsync, GivenUpMessages.Description);

        // A unique violation means somebody else just did this: the same delivery note, the same order, the
        // same idempotency key. Look again, once; the second look finds what the first one could not see.
        options.OnException<DbUpdateException>(static e => e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            .RetryOnce()
            .Then.MoveToErrorQueue()
            .And(GivenUpMessages.TellTheProcessAsync, GivenUpMessages.Description);

        options.UseMPCoreRabbitMq(new RabbitMqTransportOptions
        {
            ConnectionString = builder.Configuration["Messaging:RabbitMq:ConnectionString"]
                ?? throw new InvalidOperationException("Messaging:RabbitMq:ConnectionString is required."),
            AutoProvision = builder.Configuration.GetValue("Messaging:AutoProvision", false)
        });

        options.UseMPCoreKafka(new KafkaTransportOptions
        {
            BootstrapServers = builder.Configuration["Messaging:Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Messaging:Kafka:BootstrapServers is required."),
            AutoProvision = builder.Configuration.GetValue("Messaging:AutoProvision", false)
        });
    });

var app = builder.Build();

// Development only, and only when configured: bring the schema up to date, then list the sample
// catalog once the host has started. See Hosting/DevelopmentSetup.cs.
await DevelopmentSetup.MigrateIfRequestedAsync(app);
DevelopmentSetup.SeedCatalogWhenStarted(app);

// ADR-007 pipeline order. UseAuthentication always precedes UseAuthorization, and both follow
// UseRouting so the authenticated fallback policy sees resolved endpoint metadata.
// Forwarded headers come first, so everything after it sees the scheme and client the gateway saw.
app.UseMPCoreGatewayForwarding();
app.UseMPCoreProblemDetails();
app.UseMPCoreRequestContext();
app.UseForwardedIdentityHeaderGuard();
if (enableOpenApi && anonymousDescriptionSurface)
{
    // The UI is a static shell that a browser navigates to, so it cannot carry a bearer token, and
    // the authenticated fallback policy applies to middleware-served content as well as to mapped
    // endpoints. It is therefore mounted ahead of authentication and only in Development. Outside
    // Development it is not served at all; the document endpoint remains and stays protected.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Storefront.Commerce v1");
        options.RoutePrefix = "openapi-ui";
    });
}

app.UseRouting();
// Runs after routing so the resolved endpoint's listener binding can be checked, and before
// authentication so a misrouted call is refused without evaluating any credential.
app.UseTransportPortSeparation();
app.UseAuthentication();
app.UseAuthorization();

var grpcProbeEndpoint = app.MapGrpcService<PlatformProbeService>();
var grpcFulfillmentEndpoint = app.MapGrpcService<FulfillmentService>();
var grpcCatalogEndpoint = app.MapGrpcService<CatalogService>();
var grpcHealthEndpoint = app.MapGrpcHealthChecksService();
if (allowAnonymousHealthEndpoints)
{
    grpcHealthEndpoint.AllowAnonymous();
}
IEndpointConventionBuilder? grpcReflection = null;
if (enableGrpcReflection)
{
    // Reflection lets grpcui and grpcurl discover services without a local .proto copy. Outside
    // Development it stays behind the authenticated fallback, so enabling it on a shared host does not
    // hand the service inventory to an anonymous caller.
    grpcReflection = app.MapGrpcReflectionService();
    if (anonymousDescriptionSurface)
    {
        grpcReflection.AllowAnonymous();
    }
}

IEndpointConventionBuilder? openApiDocument = null;
if (enableOpenApi)
{
    // The document describes the whole REST surface. Anonymous only in Development; when enabled
    // elsewhere it exists but requires a token like every other endpoint.
    openApiDocument = app.MapOpenApi();
    if (anonymousDescriptionSurface)
    {
        openApiDocument.AllowAnonymous();
    }
}

var restProbeEndpoints = app.MapPlatformProbeEndpoints();
var storefrontEndpoints = app.MapStorefrontEndpoints();
var livenessEndpoint = app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = static check => check.Tags.Contains(HostHealthChecks.Live),
        ResponseWriter = WriteAggregateStatusAsync
    });
var readinessEndpoint = app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions { ResponseWriter = WriteAggregateStatusAsync });
var startupEndpoint = app.MapHealthChecks(
    "/health/startup",
    new HealthCheckOptions { ResponseWriter = WriteAggregateStatusAsync });
if (allowAnonymousHealthEndpoints)
{
    livenessEndpoint.AllowAnonymous();
    readinessEndpoint.AllowAnonymous();
    startupEndpoint.AllowAnonymous();
}

IEndpointConventionBuilder? metricsScrape = null;
if (metricsScrapeEnabled)
{
    metricsScrape = app.MapMPCorePrometheusScrape(app.Configuration.GetValue("Observability:Metrics:Prometheus:Path", "/metrics")!);
}

// Endpoints are bound to the Kestrel listener they may be served from, never to the client-supplied
// Host header. TransportEndpointGuard has already proved both ports match real listeners.
if (app.Configuration.GetValue("Transport:EnforcePortSeparation", true))
{
    var restPort = app.Configuration.GetValue("Transport:RestPort", 8080);
    var grpcPort = app.Configuration.GetValue("Transport:GrpcPort", 8081);
    grpcProbeEndpoint.RequireListenerPort(grpcPort);
    grpcFulfillmentEndpoint.RequireListenerPort(grpcPort);
    grpcCatalogEndpoint.RequireListenerPort(grpcPort);
    foreach (var group in storefrontEndpoints)
    {
        group.RequireListenerPort(restPort);
    }
    grpcHealthEndpoint.RequireListenerPort(grpcPort);
    restProbeEndpoints.RequireListenerPort(restPort);
    livenessEndpoint.RequireListenerPort(restPort);
    readinessEndpoint.RequireListenerPort(restPort);
    startupEndpoint.RequireListenerPort(restPort);
    openApiDocument?.RequireListenerPort(restPort);
    metricsScrape?.RequireListenerPort(restPort);
    grpcReflection?.RequireListenerPort(grpcPort);
}

await app.RunAsync();

// Health responses expose the aggregate status word only. Check names, dependency hosts, durations
// and exception text are never disclosed anonymously.
static Task WriteAggregateStatusAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "text/plain; charset=utf-8";
    return context.Response.WriteAsync(report.Status.ToString());
}

public partial class Program;
