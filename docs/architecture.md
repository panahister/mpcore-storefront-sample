# Architecture

Why Storefront is cut into these three backends, how they talk, and what each decision costs. Every
convention names where it comes from, so that you can read the source and disagree with it.

The layers inside one backend, the message model and the failure model are MP Core's, and are described
once in [MP Core: concepts](https://github.com/panahister/mpcore/blob/main/docs/guide/concepts.md) and in the
`docs/architecture.md` each backend carries. This document is about the system.

## 1. Three backends, two shapes

| Backend | What it owns | Shape | Why this shape |
|---|---|---|---|
| Commerce | products, stock, baskets, orders, payments | modular monolith: four modules, one host, one database | The four change together, are released together and are built by one team. One deployment keeps that cheap; the module boundary keeps the day open on which one of them leaves. |
| Fulfillment | shipments | service | Another team (the warehouse), another pace of change, and it must keep working while the shop is being deployed. |
| Analytics | sales figures | service | It reads everything and owns nothing the others need. Its load and its storage (time series) are unlike the shop's. |

The shapes are MP Core's two: `--shape modular-monolith` and `--shape service`. Sam Newman's advice
(*Monolith to Microservices*) is to start with one deployable and to split along a boundary that has
proved itself; Simon Brown's *modular monolith* is what makes that split possible later. A service here
is a bounded context (Eric Evans, *Domain-Driven Design*) that has a reason of its own to be deployed
alone.

## 2. Inside Commerce: a module writes only its own data

Commerce has four modules: Catalog, Basket, Ordering and Payments. Each is **one project** with `Domain/`,
`Application/` and `Infrastructure/` folders, and owns a database schema. A module that others talk to has
a second, small `Contracts` project with the messages it sends and accepts, and at most a read-only
interface.

| Rule | Source | What guards it |
|---|---|---|
| One project per module, layers as folders | Simon Brown, *package by component*; MP Core ADR-012 | the compiler: a module cannot see another module's project |
| A module writes only its own data | Vaughn Vernon, *Implementing Domain-Driven Design*: one aggregate per transaction, eventual consistency between them | architecture tests: a handler takes repositories of its own module only; no foreign key crosses a schema |
| Another module is told by a message that commits with the change that caused it | the transactional outbox (Chris Richardson, *Microservices Patterns*); Kamil Grzybek, *Modular Monolith with DDD* | MP Core: a handler never saves; the middleware saves the change and the messages together |
| A module may read from another through its Contracts | Microsoft eShop; MP Core ADR-012 §7 | architecture test: the Contracts projects publish no interface that writes |
| A query only reads, and is the only thing a `GET` sends | Bertrand Meyer, command-query separation; Greg Young, CQRS; RFC 9110 (a `GET` is safe) | architecture tests: a query handler declares no unit of work, publisher or repository |
| A business rule is a named class, checked by the aggregate before it changes | Kamil Grzybek; Vladimir Khorikov, *always-valid domain model* | unit tests name every refusal by its rule code |
| The shape of a request is checked before the handler runs | FluentValidation, as Wolverine middleware | architecture test: every command a caller can send has a validator |

The order process shows what the second and third rule cost. Checkout empties the basket and answers
**202 Accepted** with the identity the order will have; the order itself is created a moment later, from a
message. A problem found after that answer can only end in a cancelled order, never in a refused checkout.
[business.md](business.md), section 5, draws the process.

**The other way.** A module can also change another through a call that writes, published in that
module's Contracts, inside one transaction. It is simpler and it is atomic, and it ties the two modules
to one database for good. MP Core supports both (ADR-012 §7). This sample uses messages throughout, because
the question it answers is what it takes to keep a module able to leave.

## 3. Between the services: two brokers, two jobs

| Channel | Carries | Pattern | Why this broker |
|---|---|---|---|
| Kafka topics `storefront.ordering.order-placed.v1`, `order-paid.v1`, `order-cancelled.v1`, `order-shipped.v1`, `storefront.catalog.product-price-changed.v1` | what happened, in order, for any number of readers | Publish-Subscribe Channel | A reader that arrives later can read the stream from its start; Analytics does. The order of one order's events is kept by partitioning on the order's identity. |
| RabbitMQ queue `storefront.fulfillment.orders-ready-to-ship.v1` | work for one reader: an order to ship | Point-to-Point Channel | One consumer takes each message and acknowledges it; nothing needs replaying. |
| RabbitMQ queue `storefront.commerce.shipments-dispatched.v1` | the answer: the parcel has left | Point-to-Point Channel | the same |

The pattern names are Gregor Hohpe's and Bobby Woolf's (*Enterprise Integration Patterns*).

An order that is ready to ship is **not** the event "order paid". `OrderPaid` says what happened and
carries what every reader may know. `OrderReadyToShip` is addressed to the warehouse and carries an address
and the lines to pick; it carries no price and no payment. Each service is told what it needs and no more.

Every message between services leaves through the **outbox** of the service that sends it, in the
transaction that made the change, and enters through the **inbox** of the service that reads it. Delivery
is therefore at least once, and a reader must be able to see a message twice:

| What repeats | What stops it | Where |
|---|---|---|
| A caller retries a request | request idempotency: the key and the answer commit with the change | Commerce: `POST /v1/basket/checkout` (required), restock and payment intents (optional) |
| A broker delivers an integration event again | MP Core's inbox, by the event's identity | all three backends (`UseMPCoreInbox`) |
| The same business fact arrives in another message | a business key on the aggregate | the order's identity on `Order`, `Payment`, `StockReservation` and `Shipment`; SKU and delivery note on `StockReceipt`; the event's identity on `OrderFact` |
| News arrives about something that is already so | the receiver accepts it and changes nothing | Commerce: a shipment dispatched for an order that has already left |

The Idempotent Receiver is Hohpe's and Woolf's; the `Idempotency-Key` header is the IETF HTTPAPI working
group's draft, made common by Stripe. MP Core's part is ADR-013.

## 4. Contracts between services

The three backends share **no assembly**. A shared contracts library would make every release of one
service a release of the others. Instead:

- The service that publishes declares the message in its own code (Commerce:
  `Modules/Ordering/.../Domain/Events/`).
- A service that reads declares **its own copy**, with only the properties it uses (Fulfillment:
  `Application/Contracts/OrderReadyToShip.cs`; Analytics: `Application/Contracts/OrderEvents.cs`; Commerce:
  `Api/Hosting/FulfillmentEventsConsumer.cs`).
- The two agree on three things: the channel's name, the contract's name and version, and the JSON.
- There is one channel per contract, and the listener says which type it reads:
  `ListenToKafkaTopic(topic).DefaultIncomingMessage<OrderPaid>()`.
- [`tests/Storefront.Contracts.Tests`](../tests/Storefront.Contracts.Tests) serializes what each publisher
  sends and reads it with each reader's copy. It is the only project that references all three backends.

This is Ian Robinson's *consumer-driven contracts*. Between teams that do not share a repository the same
test is a Pact; here it is a unit test. A reader's copy that ignores properties it does not know is the
*tolerant reader* (Martin Fowler): a publisher may add a property without asking anybody.

A version is part of the channel's name (`.v1`). A change that breaks a reader is a new contract on a new
channel, published next to the old one until its last reader has moved.

## 5. Transports

| Backend | REST | gRPC | Why |
|---|---|---|---|
| Commerce | `:5100`, for the storefront and the back office | `:5101`, for other systems | Both, on two listeners. An endpoint is bound to its listener, so a gRPC method cannot be reached on the REST port. |
| Fulfillment | none | `:5201` | Its callers are systems in the warehouse. |
| Analytics | `:5300` | none | Its callers are dashboards. |

A failure looks the same on both transports: a category, an error domain, a code, a message key and, for
validation, one violation per field. REST carries it as Problem Details (RFC 9457) and gRPC as a rich
status. A business rule that is broken answers under its own code: `422` and `FailedPrecondition`.

Health has two questions on both transports: alive (the process answers) and ready (the database
answers). `Hosting/HostHealthChecks.cs` in each backend says what is asked; the distinction is Kubernetes'
liveness and readiness probes.

## 6. Security

One Keycloak realm, `storefront`. Every backend is an OAuth 2.0 resource server with its own **audience**:
a token names the services it may be shown to, and a service refuses a token that does not name it.

| Caller | How it signs in | Audiences of its token | Roles |
|---|---|---|---|
| A shopper or a member of staff, in the web client `storefront-web` | the password grant, so that a script can sign in; a real web client uses the authorization code flow with PKCE | Commerce, Analytics | `customer`, `catalog-manager`, `support-agent`, `analyst` |
| The warehouse system, client `storefront-warehouse` | client credentials | Commerce, Fulfillment | `warehouse` |

Authorization is deny by default: an endpoint without a policy requires a token, and the only anonymous
endpoints are the health probes, the catalog pages and, in Development, the description of the API. A role is checked by a named policy
(`Hosting/*Policies.cs`). What a caller may see of another's data is a business decision and lives in the
application layer (`OrderAccess`): another shopper's order is "not found", never "forbidden".

## 7. When something fails

| What fails | What happens | Where to see it |
|---|---|---|
| A handler answers a failure after it changed something | the transaction is rolled back; nothing is saved, nothing is published | scenario S2 |
| A save loses a race (optimistic concurrency) | the message is tried again after a pause that grows and is lengthened at random (Marc Brooker, *Exponential Backoff And Jitter*) | scenarios S15, S16 |
| An attempt fails after it published | its messages are discarded with it | scenario S15 |
| A message is given up after its retries | it goes to the error queue, and the process that waited for its answer is told, so no order waits for ever. The error queue of a local queue is the table `wolverine.wolverine_dead_letters`; of a RabbitMQ queue, the broker's `wolverine-dead-letter-queue` | scenarios S16, S18; `Hosting/GivenUpMessages.cs` |
| The payment provider is down | the HTTP client retries, then the message is redelivered with a cooldown; the order stays "awaiting payment" | scenarios S6, S7 |
| A business rule is broken by a queued message | it is never retried: the answer would be the same | MP Core |

## 8. Observability

Every backend sends logs, metrics and traces over OTLP to one OpenTelemetry Collector. A trace crosses the
services: the checkout request, the handlers of the order process, the SQL, the message to the warehouse
and its handler there are spans of one trace, because the trace context travels in the message headers
(W3C Trace Context). Open Jaeger after a scenario run and look for a trace that names three services.

## 9. What this sample does not show

- **Multi-tenancy.** Every backend reads a tenant from the token when there is one; Storefront has one
  tenant.
- **A module that changes another through a call that writes.** Section 2 says why.
- **A schema registry, or a contract in Avro or Protobuf on the broker.** The messages are JSON.
- **Deployment.** There is no container image, chart or pipeline that deploys. MP Core generates none, on
  purpose: how a backend is deployed belongs to the platform it runs on.
- **A saga with a deadline.** The order process answers when a step is given up, but has no timer.
  MP Core has no scheduling port yet.
