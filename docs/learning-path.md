# A path through the code

Thirteen steps, each one idea. A step names what to read, what to run, and what to try yourself. Read them in
order the first time: each builds on the one before. The whole path takes a day.

Paths that start with a module's name are in Commerce, inside that module's one project:
`commerce/src/Modules/<Module>/Storefront.Commerce.Modules.<Module>/`. Start the system first
([running.md](running.md)).

Every step ends with the **skill** an AI coding agent would use for that kind of work. The skills are in
each backend's `.mpcore/skills`, and they are worth reading as a person: each is the procedure, with what
must be asked and what must not be decided alone.

## 1. A business rule is a class with a name

| | |
|---|---|
| Read | `Catalog/Domain/Rules/PriceMoveMustBeGradual.cs`, then `Catalog/Domain/Product.cs`, the method `ChangePrice` |
| Run | `scripts/scenarios.sh S3` |
| See | The answer is 422 with the rule's own code and the numbers that broke it. The attempt is in the audit trail, although nothing was changed. |
| Try | Add a rule: a price may not end in `.99`. Write the test first, in `commerce/tests/Storefront.Commerce.Tests/Unit/DomainTests.cs`. |
| Sources | Kamil Grzybek, *Modular Monolith with DDD* (named rules); Vladimir Khorikov, *always-valid domain model* |
| Skill | `mpcore-implement-vertical-slice` |

## 2. A value object cannot be wrong

| | |
|---|---|
| Read | `Catalog/Domain/Price.cs`, `Ordering/Domain/PhoneNumber.cs` |
| See | `Price.Of` checks and returns; there is no way to hold a `Price` that the business would refuse. The column in the database is a plain number: the mapping converts. |
| Try | Find where a `Price` becomes a column: `Catalog/Infrastructure/ProductConfiguration.cs`. |
| Sources | Eric Evans, *Domain-Driven Design* (value objects); Martin Fowler, *Money* |
| Skill | `mpcore-implement-vertical-slice` |

## 3. A handler never saves

| | |
|---|---|
| Read | `Catalog/Application/Commands/ChangeProductPrice.cs` |
| See | The handler is a static method. It takes its ports as parameters, changes the aggregate, and returns. It declares `IUnitOfWork` and never calls it: MP Core saves after the handler returns, and commits the change together with every message the handler published. |
| Try | Put a breakpoint in the handler and step out of it, into MP Core. With a clone of MP Core next to this repository the debugger follows. |
| Sources | MP Core ADR-011; the transactional outbox (Chris Richardson, *Microservices Patterns*) |
| Skill | `mpcore-implement-vertical-slice` |

## 4. Three kinds of check, three answers

| | |
|---|---|
| Read | `Basket/Application/Validators/CheckoutValidator.cs` (the shape of a request), `Ordering/Domain/Rules/` (the business), `commerce/src/Storefront.Commerce.Api/Hosting/StorefrontPolicies.cs` (who may) |
| Run | `scripts/scenarios.sh S11` |
| See | 400 with one violation per field, before the handler ran. 422 for a rule. 403 for a role, and 404, not 403, for another shopper's order. |
| Sources | RFC 9457 (Problem Details); MP Core ADR-006, ADR-008 |
| Skill | `mpcore-apply-security`, `mpcore-design-transport-contract` |

## 5. A query only reads

| | |
|---|---|
| Read | `Basket/Application/Queries/`, and next to it `Basket/Application/Commands/AcknowledgeBasketPrices.cs` |
| See | Reading the basket changes nothing, however often it is read. Saying "I have seen the new price" is a command of its own. A query reads through a read-model port that returns views, never through the repository. |
| Try | Make a query handler take `IUnitOfWork`, and run the tests. `ModuleStructureTests` refuses it. |
| Sources | Bertrand Meyer (command-query separation); Greg Young (CQRS); RFC 9110 (a `GET` is safe) |
| Skill | `mpcore-implement-vertical-slice` |

## 6. A module tells another module in a message

| | |
|---|---|
| Read | `Basket/Application/Commands/Checkout.cs`, then `Ordering/Application/Process/OrderProcessHandler.cs` from top to bottom |
| Run | `scripts/scenarios.sh S1`, then open Jaeger and find the trace of the checkout |
| See | Checkout changes the Basket and nothing else. The order is created from a message, the payment registered from another, the stock reserved from a third. Each step is one transaction in one module. The answer to the shopper is 202. |
| Try | Read [business.md](business.md), section 5, with the trace next to it. |
| Sources | Vaughn Vernon, *Implementing Domain-Driven Design* (one aggregate per transaction); Gregor Hohpe and Bobby Woolf, *Enterprise Integration Patterns* (process manager) |
| Skill | `mpcore-integrate-contexts`, `mpcore-plan-bounded-context` |

## 7. What another module may use

| | |
|---|---|
| Read | the project `commerce/src/Modules/Catalog/Storefront.Commerce.Modules.Catalog.Contracts` |
| See | Messages, and one interface that only reads, `ICatalogLookup`. Nothing else of the Catalog is visible to another module: the compiler refuses. |
| Try | From the Basket, use `Product`. It does not compile. |
| Sources | Simon Brown, *package by component*; MP Core ADR-012 |
| Skill | `mpcore-implement-ddd-module` |

## 8. When a request arrives twice

| | |
|---|---|
| Read | `commerce/src/Storefront.Commerce.Api/Rest/Endpoints/StorefrontEndpoints.cs`, the checkout endpoint; `Catalog/Domain/StockReceipt.cs` |
| Run | `scripts/scenarios.sh S14` |
| See | The same checkout sent twice runs once; the second answer is the first, marked `Idempotency-Replayed: true`. The same delivery note entered twice adds the stock once, although the two requests had two different keys: there the business key decides. |
| Sources | The `Idempotency-Key` header (IETF HTTPAPI working group; Stripe); Brandur Leach on its implementation with PostgreSQL; MP Core ADR-013 |
| Skill | `mpcore-implement-vertical-slice` |

## 9. When many requests arrive at once

| | |
|---|---|
| Read | `commerce/src/Storefront.Commerce.Api/Program.cs`, the rules that start with `OnException`; `commerce/src/Storefront.Commerce.Api/Hosting/GivenUpMessages.cs` |
| Run | `scripts/scenarios.sh S15 S16` |
| See | Eight checkouts of one basket at the same moment: one order, one charge. Twelve orders for one product within seconds: every one comes to an end. |
| Try | Read [lessons.md](lessons.md), sections 1 and 3, for what these two scenarios found when they were first run. |
| Sources | Marc Brooker, *Exponential Backoff And Jitter* |
| Skill | `mpcore-verify-business-behavior` |

## 10. A secret stays where it belongs

| | |
|---|---|
| Read | `Payments/Domain/PaymentIntent.cs`, `Payments/Application/Commands/RegisterPaymentHandler.cs` |
| Run | `scripts/scenarios.sh S17` |
| See | The card token goes to Payments and nowhere else. Checkout and every message carry the identity of a payment intent. No queue table and no log line holds a token. |
| Try | Find the test that builds every message and reads what it prints: `A_message_never_prints_its_secret`. |
| Sources | The payment intent of the card providers' APIs |
| Skill | `mpcore-apply-observability`, `mpcore-apply-security` |

## 11. Two services and no shared code

| | |
|---|---|
| Read | `Ordering/Domain/Events/OrderReadyToShip.cs` (what Commerce sends); `fulfillment/src/Storefront.Fulfillment.Application/Contracts/OrderReadyToShip.cs` (what the warehouse reads); `tests/Storefront.Contracts.Tests/ContractTests.cs` (what holds them together) |
| Read | `fulfillment/src/Storefront.Fulfillment.Api/Program.cs`, the listener and the route; `commerce/src/Storefront.Commerce.Api/Hosting/FulfillmentEventsConsumer.cs`, the answer |
| Run | `scripts/scenarios.sh S18`, then open RabbitMQ's management page and Jaeger |
| See | The warehouse learns of an order from a queue, is asked over gRPC to dispatch it, and tells the shop on a second queue. The warehouse was never told a price. A token issued for the shop is refused by the warehouse. |
| Try | Add a property to what Commerce sends. Nothing else has to change. Remove one the warehouse reads, and the contract test fails. |
| Sources | Ian Robinson, *consumer-driven contracts*; Martin Fowler, *tolerant reader*; Hohpe and Woolf, *Point-to-Point Channel* |
| Skill | `mpcore-integrate-contexts`, `mpcore-configure-messaging` |

## 12. A stream, read at its own pace

| | |
|---|---|
| Read | `analytics/src/Storefront.Analytics.Application/Events/OrderEventsHandler.cs`, `analytics/src/Storefront.Analytics.Infrastructure/Persistence/SalesReadModel.cs`, and the first migration next to it |
| Run | `scripts/scenarios.sh S19` |
| See | Analytics reads three Kafka topics from their start, keeps each event as one fact in a hypertable, and answers two reports with `time_bucket`. It can be stopped for an hour and will catch up. Commerce does not know that it exists. |
| Try | Stop Analytics, run `scripts/scenarios.sh S1`, start Analytics, and ask for the report. |
| Sources | Hohpe and Woolf, *Publish-Subscribe Channel*; Martin Kleppmann, *Designing Data-Intensive Applications* (a log as the source of derived data) |
| Skill | `mpcore-configure-messaging` |

## 13. Behind a gateway

| | |
|---|---|
| Read | `infrastructure/apisix/apisix.template.yaml`, the routes of the edge; `commerce/src/Storefront.Commerce.Api/Program.cs`, the lines around `UseMPCoreGatewayForwarding`; `Gateway:TrustedProxies` in `appsettings.Development.json` |
| Run | `scripts/scenarios.sh S20` |
| See | REST and gRPC of three backends through one door, over TLS. The backend knows the public address from the gateway, and believes it because the gateway is a trusted proxy. It never believes the gateway about who the caller is: the token says that. |
| Try | Replace the entries of `Gateway:TrustedProxies` with an address that is not the gateway's, start Commerce, and ask `/openapi/v1.json` through the edge. The address it names now begins with `http://`: the backend no longer believes what it is told about the scheme. |
| Sources | MP Core ADR-007 and ADR-009; NIST SP 800-207 (never trust, always verify) |
| Skill | `mpcore-apply-security` |

## And then

- **The tests as a specification.** `commerce/tests/Storefront.Commerce.Tests/Architecture/ModuleStructureTests.cs`
  is the architecture written as assertions: read its test names from top to bottom.
- **Operating it.** Stop PostgreSQL (`docker stop storefront-postgres-1`) and ask Commerce whether it is
  alive (`/health/live`) and whether it is ready (`/health/ready`). Read
  `commerce/src/Storefront.Commerce.Api/Hosting/HostHealthChecks.cs` for why the answers differ.
- **A backend of your own.** Generate one with the options of the backend here that is nearest to what you
  need ([mpcore-coverage.md](mpcore-coverage.md), section 1), and keep this repository open next to it.
