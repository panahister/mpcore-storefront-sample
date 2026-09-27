# What of MP Core this sample shows, and where

Three tables: the choices the generator offers, the packages, and the behaviours worth seeing live. The
last section says what is **not** shown, so that nobody looks for it.

Paths are relative to the backend named in the row. In Commerce, a path that starts with a module's name
is inside that module's one project, `src/Modules/<Module>/Storefront.Commerce.Modules.<Module>/`.

## 1. The generator's choices

Each backend was generated with `mpcore new backend` and then written into. The choices are recorded in
each backend's `.mpcore/template-manifest.json`.

| Choice | Values | Commerce | Fulfillment | Analytics |
|---|---|---|---|---|
| `--shape` | `service`, `modular-monolith` | `modular-monolith` | `service` | `service` |
| `--transport` | `rest`, `grpc`, `both` | `both` | `grpc` | `rest` |
| `--messaging` | `kafka`, `rabbitmq`, `none` | `kafka`, and RabbitMQ added by hand | `rabbitmq` | `kafka` |
| `--cache` | `none`, `memory`, `redis`, `hybrid` | `hybrid` | `none` | `memory` |
| `--business-audit` | `none`, `postgresql` | `postgresql` | `postgresql` | `none` |
| `--timeseries` | `none`, `timescale` | `none` | `none` | `timescale` |
| `--ai-tooling` | `none`, `codex`, `claude`, `both` | `both` | `both` | `both` |

Every value is used by a backend, except `--messaging none` and `--cache redis`. A backend that talks to
nobody would show nothing, and the hybrid cache is the Redis adapter with an in-process level in front of
it.

**Two brokers in one host.** The generator offers one. Commerce was generated with Kafka; RabbitMQ was
added with one package reference and one call, `options.UseMPCoreRabbitMq(...)`, in
`src/Storefront.Commerce.Api/Program.cs`.

## 2. Packages

| Package | What is used | Where |
|---|---|---|
| `MPCore.Domain` | `AggregateRoot<TId>`, `Entity<TId>`, `ValueObject`, `BusinessRule` and `CheckRule`, `IntegrationEvent`, `Raise` | every `Domain/` folder; Commerce `Catalog/Domain/Product.cs`, `Ordering/Domain/Order.cs`; Fulfillment `Shipment.cs`; Analytics `OrderFact.cs` |
| `MPCore.Application` | `ICommand<T>`, `IQuery<T>`, `Result<T>`, `FailureDescriptor`, `ErrorCategory`, `RetryDirective`, `IClock`, `PageRequest`, `Page<T>`, `SortAllowlist`, `IIdempotentExecutor` | every file in `Application/Commands` and `Application/Queries`; every `*Failures.cs`; the executor in Commerce `Rest/Endpoints/StorefrontEndpoints.cs` |
| `MPCore.Persistence.Abstractions` | `IRepository<T,TId>`, `IUnitOfWork` (declared by every handler that writes, never called) | `Application/Ports/`; handlers |
| `MPCore.Persistence.EntityFrameworkCore.PostgreSql` | `MPCoreDbContext`, `PostgreSqlDbContextOptions.Apply` | `Infrastructure/Persistence/AppDbContext.cs`, `Infrastructure/DependencyInjection.cs` |
| `MPCore.Persistence.Timescale` | `EnsureTimescale`, `CreateHypertable` in a migration | Analytics `Infrastructure/Migrations/*_InitialAnalytics.cs` |
| `MPCore.Messaging.Abstractions` | `IMessagePublisher` | Commerce `Basket/Application/Commands/Checkout.cs`, `Ordering/Application/Process/OrderProcessHandler.cs` |
| `MPCore.Messaging.Wolverine` | `UseMPCoreWolverine<AppDbContext>`, `AddMPCoreWolverineDbContext`, `DiscoverHandlersIn`, `UseMPCoreInbox` | `Api/Program.cs`, `Api/Hosting/HandlerAssemblies.cs`, `Infrastructure/DependencyInjection.cs` |
| `MPCore.Messaging.Wolverine.Kafka` | `UseMPCoreKafka`; routes, listeners, partition keys | Commerce `Api/Program.cs`, `Api/Hosting/CommerceTopics.cs`; Analytics `Api/Program.cs`, `Api/Hosting/AnalyticsTopics.cs` |
| `MPCore.Messaging.Wolverine.RabbitMQ` | `UseMPCoreRabbitMq`; a durable route and a durable listener | Commerce `Api/Hosting/CommerceQueues.cs`; Fulfillment `Api/Hosting/FulfillmentQueues.cs`, `Api/Program.cs` |
| `MPCore.Idempotency.EntityFrameworkCore.PostgreSql` | `AddMPCoreIdempotency`, `UseMPCoreIdempotency`, `ApplyMPCoreIdempotency` | `Infrastructure/DependencyInjection.cs`, `AppDbContext.cs`, in all three |
| `MPCore.Validation.FluentValidation` | `UseMPCoreFluentValidation`, `AddMPCoreValidators` | `Api/Program.cs`; every `Application/Validators/*Validator.cs` |
| `MPCore.Localization` | `AddMPCoreMessageCatalog`; resource files in English and Persian | `Api/Program.cs`; every `Resources/*Messages.resx` and `.fa.resx` |
| `MPCore.Localization.EntityFrameworkCore.PostgreSql` | translations edited at run time | Commerce `Api/Hosting/TranslationHandlers.cs`, `/v1/backoffice/translations` |
| `MPCore.Caching.Abstractions` | `IReadThroughCache.GetOrCreateAsync`, `ICache.RemoveAsync` | Commerce `Catalog/Application/Queries/GetProductDetails.cs`, `Catalog/Application/Events/ProductUpdatedHandler.cs`; Analytics `Application/Queries/SalesReports.cs` |
| `MPCore.Caching.Hybrid` | `AddMPCoreHybridCache` | Commerce `Infrastructure/DependencyInjection.cs` |
| `MPCore.Caching.Memory` | `AddMPCoreMemoryCache` | Analytics `Infrastructure/DependencyInjection.cs` |
| `MPCore.Audit.Abstractions` | `IBusinessAuditRecorder`, `AuditPolicy`, `IAuditQuery` | Commerce `Catalog/Application/Commands/ChangeProductPrice.cs`, `/v1/backoffice/audit`; Fulfillment `Application/Commands/DispatchShipment.cs`; `Infrastructure/Audit/AuditPolicyConfiguration.cs` in both |
| `MPCore.Audit.EntityFrameworkCore.PostgreSql` | `AddMPCoreAudit`, `UseMPCoreAudit`, `ApplyMPCoreAudit` | Commerce and Fulfillment `Infrastructure/DependencyInjection.cs` |
| `MPCore.Security.Abstractions` | `ICurrentActorAccessor`, `CurrentActor.HasRole`, `SystemActorScope` | Commerce `Basket/Application/Commands/Checkout.cs`, `Ordering/Application/OrderAccess.cs`, `Api/Hosting/DevelopmentSetup.cs` |
| `MPCore.Security.AspNetCore` | the bearer resource server, Keycloak claim mapping, default-deny authorization, policy contributors, forwarded headers from trusted proxies, the guard against forged identity headers | `Api/Program.cs`; `Api/Hosting/StorefrontPolicies.cs`, `FulfillmentPolicies.cs`, `AnalyticsPolicies.cs` |
| `MPCore.Transport.Http` | `ToHttpResult`, Problem Details, `Accept-Language`, `RequireIdempotencyKey` | Commerce `Api/Rest/Endpoints/StorefrontEndpoints.cs`; Analytics `Api/Rest/Endpoints/SalesEndpoints.cs` |
| `MPCore.Transport.Grpc` | a failure as a rich gRPC status | Commerce `Api/Grpc/Services/CommerceGrpcServices.cs`; Fulfillment `Api/Grpc/Services/ShipmentsService.cs` |
| `MPCore.Resilience.Http` | `AddMPCoreResilientHttpClient`: timeouts, retries, a circuit breaker | Commerce `Payments/Infrastructure/PaymentsModule.cs` |
| `MPCore.Observability` | OpenTelemetry logs, traces and metrics, with redaction | `Api/Program.cs`, `appsettings.Development.json`, in all three |
| `MPCore.Hosting` | `AddMPCoreFoundation` | `Api/Program.cs`, in all three |
| `MPCore.Tenancy.Abstractions` | `AddMPCoreTenancyFromClaim` | `Api/Program.cs`, in all three; see section 4 |
| `MPCore.Observability.Prometheus` | the scrape endpoint | generated in Commerce and Analytics, and off; see section 4 |
| `MPCore.Cli`, `MPCore.Templates` | the generator | each backend's `.mpcore/template-manifest.json` |

`MPCore.Caching.Redis` is the only runtime package no backend references by name; the hybrid cache brings
it.

## 3. Behaviours worth seeing live

| Behaviour | Scenario | Where to look afterwards |
|---|---|---|
| A transaction changes one module, and the next module hears of it in a message | S1 | Jaeger: one checkout trace, the handlers of the order process in order |
| A failure answered after a change rolls the change back | S2 | the basket still holds its line after the refused checkout |
| An integration event leaves through the outbox and is read through the inbox | S2, S18, S19 | table `idempotency.processed_messages` in each database |
| A broken business rule answers under its own code, and the refused attempt is audited | S3 | `GET /v1/backoffice/audit?module=catalog` |
| A business answer instead of an exception; compensation | S4, S5 | the order's history |
| The resilient HTTP client; a retryable failure is redelivered, then given up | S6, S7 | WireMock's request log; `wolverine.wolverine_dead_letters` |
| A domain event is handled after the commit | S8 | the restock alerts |
| gRPC next to REST in one host, each on its own port | S9 | `grpcurl` against `:5101`, and against `:5100` |
| 401, 403, and 404 instead of 403; input validation answers 400 before the handler runs | S11 | the Problem Details |
| Events on Kafka, partitioned by order and by SKU | S12 | Kafka UI |
| Messages in the caller's language; a text edited at run time | S13, S19 | `Accept-Language: fa` |
| A request sent twice with one `Idempotency-Key` runs once and is answered twice | S14 | the header `Idempotency-Replayed: true`; table `idempotency.requests` |
| A message published by an attempt whose save failed is never delivered | S15 | eight checkouts at once, one order, one charge |
| Orders that collide on one row are retried apart from each other, and all come to an end | S16 | `Api/Program.cs`, the rule for `DbUpdateConcurrencyException` |
| A secret stays where it belongs | S17 | no queue table holds a card token |
| Two services with no shared code; a token's audience | S18 | RabbitMQ's management page; Jaeger: a trace that names two services |
| A time series in a hypertable; a cached report | S19 | `timescaledb_information.hypertables`; `Application/Queries/SalesReports.cs` |
| A backend behind a gateway: what it believes, and what it does not | S20 | the API description names the edge's address; a forged `X-Forwarded-User` changes nothing; `Api/Program.cs`, `UseMPCoreGatewayForwarding` |
| A gateway that verifies tokens does not replace the backend's own verification | S21, with `EDGE_AUTH=keycloak` | who answered each refusal: the edge, or the backend as Problem Details |
| Alive and ready are two questions | none: stop PostgreSQL and ask | `/health/live` stays `Healthy`, `/health/ready` answers 503; over gRPC, service `live` and the empty service name |

## 4. What is not shown

| Capability | State here |
|---|---|
| Multi-tenancy | Every backend reads the tenant from the token's `tenant_id` claim when there is one, and the audit trail records it. Storefront has one tenant and no token carries the claim. |
| The Prometheus scrape endpoint | Generated in the two backends that serve REST, and off. The backends push metrics over OTLP instead. Set `Observability:Metrics:Prometheus:Enabled` to turn it on; the endpoint then requires a token. |
| `--messaging none`, `--cache redis` | No backend uses them; section 1 says why. |
| A call through Contracts that writes | Supported by MP Core (ADR-012 §7). This sample uses messages between modules throughout. |
| A partition key or a delay set by the publisher | `IMessagePublisher` has neither yet. Commerce sets the Kafka key with Wolverine's partitioning rules in its host. |

## 5. Generated, and written

Generated and left as generated: the solution layout, `Directory.Build.props`, the `Api/Hosting` files
`TransportEndpointGuard.cs`, `TransportPortSeparation.cs`, `DeveloperEndpoints.cs`,
`HostHealthChecks.cs` and `AppDbContextDesignTimeFactory.cs`, the platform probe, each backend's `README.md` and `docs/`, and the AI skills.

Generated and then extended: `Program.cs` (routes, listeners, retry rules, the inbox, policies, endpoints,
the message catalog, the validators, the second language), `HandlerAssemblies.cs`,
`Infrastructure/DependencyInjection.cs`, `AppDbContext.cs`, `AuditPolicyConfiguration.cs`, the `Api`
project file and `appsettings.Development.json`.

Written: everything under a `Domain`, `Application` or `Modules` folder, the mappings, read models and
migrations, the endpoints and gRPC services, the tests, `scripts/`, `infrastructure/` and these documents.
