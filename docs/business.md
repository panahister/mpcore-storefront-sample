# The business

Which business Storefront is, who uses it, which rules it keeps, which stories the scenarios tell, and
where each of these is in the code. Why the system is cut into three backends is in
[architecture.md](architecture.md).

## 1. The business

**Storefront** is a fictional online store for outdoor gear: tents, sleeping bags, backpacks, boots,
stoves and clothing. Customers buy online and pay by card through **DemoPay**, a fictional payment
provider. A warehouse picks, packs and hands the parcels to a carrier. The company reads its sales per
hour and per city.

| Backend | Its part of the business |
|---|---|
| **Commerce** | The storefront: catalog and stock, basket, ordering, payment |
| **Fulfillment** | The warehouse: what is to be shipped, and what has left |
| **Analytics** | The figures: orders placed, paid and cancelled, over time |

Sections 2 to 8 describe Commerce, where most of the rules are. Sections 9 and 10 describe the other two.

## 2. Roles

| Role (Keycloak realm role) | User in the demonstration | Does |
|---|---|---|
| — (anonymous) | — | Browses the catalog and product pages |
| `customer` | `sara`, `reza` | Keeps a basket, places orders, reads and cancels their own orders |
| `catalog-manager` | `mina` | Lists products, changes prices, restocks, withdraws products, reads restock alerts |
| `support-agent` | `ali` | Reads every order, cancels any unshipped order with a reason, reads the audit trail |
| `warehouse` | service `storefront-warehouse` (client credentials) | Lists what is to be shipped and reports what has left, over gRPC |
| `analyst` | `nora` | Reads the sales figures |

Each password is `<name>-lab`, for example `sara-lab`. They exist for the demonstration only.

## 3. Modules

Commerce is a **modular monolith**: one host and one database, with four bounded contexts that each own a
schema. **A module writes only its own data.** What another module has to do, it is told in a message
that commits with the change that caused it; it does it in a transaction of its own. A module may read
from another through that module's Contracts (the Basket asks the Catalog for a price). No foreign key
crosses a schema.

| Module | Schema | Responsibility |
|---|---|---|
| **Catalog** | `catalog` | Products, prices, warehouse stock, stock reservations, stock receipts, restock alerts |
| **Basket** | `basket` | Each customer's basket, and checkout |
| **Ordering** | `ordering` | The order and its process, from placing to shipping or cancellation |
| **Payments** | `payments` | Each order's payment through DemoPay, and refunds |

Each module is **one project** with `Domain/`, `Application/` and `Infrastructure/` folders, plus a
`Contracts` project holding only what other modules may use (ADR-012). Every rule in section 4 is a named
class under `Domain/Rules/`, checked by the aggregate or value object before it changes state; a broken
rule answers the caller with the rule's code (HTTP 422). The shape of a request (required fields, lengths,
formats) is checked by a validator before the handler runs and answers 400 with one violation per field.
A query only reads: it lives in `Application/Queries/`, reads through a read-model port that returns views, and is
the only thing an HTTP `GET` sends. Every failure message is a key, rendered in the caller's language (`Accept-Language: fa` or `en`); support
can change a text at run time (scenario S13).

## 4. Business rules

### Catalog

| Code | Rule | Where |
|---|---|---|
| C1 | A SKU is unique, 3–32 characters of A–Z, 0–9 and hyphen, stored upper-case. | `Sku` value object (`SkuMustMatchFormat`), index `ux_products_sku`, `ListProductValidator` |
| C2 | A price is a positive amount in dollars and cents, at most 100,000. | `Price` value object (`PriceMustBePositive`, `PriceMustBeInCents`, `PriceMustNotExceedCeiling`) |
| C3 | One price change may not move the price by more than 50%. This guards against typing mistakes, and a refused attempt is audited. | `PriceMoveMustBeGradual`, checked in `Product.ChangePrice` |
| C4 | Every price change publishes `ProductPriceChanged` to Kafka and raises the price version. | `Product.ChangePrice` raises `ProductPriceChanged` (Kafka) and `ProductUpdated` |
| C5 | A discontinued product is final: never sold, repriced or restocked again. | `ProductMustBeActive`, checked in `Product.ChangePrice`, `Restock`, `Discontinue`, `Reserve` |
| C6 | Only available stock (`OnHand − Reserved`) can be promised to a new order. | `StockMustBeAvailable`, `ReservedUnitsMustCover`, checked in `Product.Reserve`, `Release`, `CommitShipment` |
| C7 | A reservation is all or nothing: if one line is short, nothing is reserved. | `ReserveStockHandler`: every line is asked with `CanReserve` before any is reserved |
| C8 | Reserve, release and commit are idempotent, keyed by order. A release that arrives before its reservation voids it. | `StockReservation`, keyed by the order; `ReserveStockHandler`, `ReleaseStockHandler`, `CommitStockHandler` |
| C9 | When available stock falls to the reorder threshold, one restock alert is raised, not one per sale. | `Product.Reserve` raises `StockFellBelowThreshold`; `StockFellBelowThresholdHandler` |
| C10 | A product page is cached and evicted after every change, after commit. | `GetProductDetailsHandler`, `ProductUpdatedHandler` |
| C11 | A product belongs to one of the store's six categories. | `CategoryMustBeKnown`, checked in `Product.List`; `BrowseProductsValidator` |
| C12 | Stock moves in whole positive units. | `QuantityMustBePositive`; `RestockProductValidator` |
| C13 | Repricing to the current price is refused: a mistake, not a change. | `PriceMustChange`, checked in `Product.ChangePrice` |
| C14 | A delivery note is received once per product, however often it is reported. Reported again with the same quantity, it answers the current stock and adds nothing; with another quantity it is refused. Upper and lower case are the same note. | `StockReceipt`, keyed by SKU and reference (`ux_stock_receipts_sku_reference`); `DeliveryMustMatchItsReceipt`; `RestockProductHandler` |

### Basket

| Code | Rule | Where |
|---|---|---|
| B1 | One basket per customer, identified by the token's `sub`, never by anything the client sends. | `SetBasketItemHandler` (the buyer is the token's subject); `Basket` keyed by buyer |
| B2 | 1–10 units per line; at most 20 different products. | `QuantityMustBeWithinLineLimit`, `BasketMustHaveRoomForAnotherLine`, checked in `Basket.SetQuantity`; `SetBasketItemValidator` |
| B3 | Every price comes from the catalog, never from the request. | `ICatalogLookup` (the Catalog's Contracts), `SetBasketItemHandler` |
| B4 | A price change from Kafka reprices every basket holding the product. The old price stays marked until the customer says they have seen it; reading the basket changes nothing. An older or repeated change is ignored. | `ApplyCatalogPriceChangeHandler`, `Basket.ApplyPriceChange`, `BasketLine.PreviousUnitPrice`; `GetMyBasketHandler` (query), `AcknowledgeBasketPricesHandler` (command) |
| B5 | Only a product the Catalog says is for sale can be added. Removing a line that is not there changes nothing, so the storefront's DELETE is idempotent. | `ProductMustBeSellable`; `Basket.SetQuantity` |
| B6 | Checkout empties the basket and announces what it held (`BasketCheckedOut`), in one transaction. The answer is "accepted", with the identity the order will have. | `CheckoutHandler`, `Basket.TakeForCheckout`, `BasketCheckedOut` (the Basket's Contracts) |
| B7 | The customer sends the total they saw. If the basket no longer costs that (a price changed), checkout is refused and the basket is left untouched. An empty basket cannot be checked out. | `CheckoutHandler`, `BasketFailures.TotalChanged`, `BasketEmpty`; MP Core's rollback policy |
| B8 | Checkout pays with a payment intent the shopper created with Payments, and refuses one that cannot pay (unknown, another shopper's, used or expired). The address must be one Ordering will accept (rule O3). | `CheckoutHandler` asks `IPaymentIntentLookup` (Payments' Contracts, a read); `CheckoutAddressValidator`; `CheckoutContractTests` |
| B9 | A checkout is sent with an `Idempotency-Key`. Sent again with the same key, it receives the first answer and nothing happens twice; the same key with another request is refused. | `StorefrontEndpoints.cs` (`RequireIdempotencyKey`, `IIdempotentExecutor`); MP Core ADR-013 |
| B10 | A basket is checked out once, however many requests arrive at the same moment. The others are refused with `BASKET_EMPTY`, and nothing they published is delivered. | `BasketLine` rows under optimistic concurrency; the retry rule in `Program.cs`; MP Core's `HandlerAttemptMiddleware` |

### Ordering

| Code | Rule | Where |
|---|---|---|
| O1 | A checked-out basket becomes an order, under the identity the Basket gave it. The same checkout delivered twice creates one order. | `OrderProcessHandler.Handle(BasketCheckedOut)`, `Order.Place` |
| O2 | The order's steps follow one another, each in its own transaction: the charge is registered, then the stock is requested, then the charge is collected. | `OrderProcessHandler`: `RegisterPayment`, `PaymentRegistered` → `ReserveStock`, `StockReserved` → `AuthorizePayment` |
| O3 | An address the courier can use anywhere: a phone number in the international format (ITU-T E.164: a plus sign, the country code and the number) and a postal code of 3 to 12 letters and digits. | `PhoneNumber` and `PostalCode` value objects (`PhoneNumberMustBeInternational`, `PostalCodeMustBeWellFormed`), `ShippingAddress` |
| O4 | (moved to the Basket: rule B8) | |
| O5 | Statuses: Submitted, then AwaitingPayment, then Paid, then Shipped. Cancelled is reachable from every status before Shipped. | `Order.Move`, `OrderMustBeInStatus`, `OrderMustNotBeCancelled`, `OrderMustNotBeShipped` |
| O6 | Cancelling a paid order refunds it; cancelling before payment voids it. In both cases the stock goes back. | `Order.Cancel` (says whether a refund is due), `CancelOrderHandler`, `OrderProcessHandler` |
| O7 | A customer sees and cancels only their own orders. Another customer's order is "not found", never "forbidden". | `OrderAccess`, `GetOrderHandler`, `ListMyOrdersHandler` |
| O8 | Support cancels any unshipped order, and must give a reason. | `CancelOrderHandler` (`OrderingFailures.NoteRequired`), `OrderMustNotBeShipped` |
| O9 | Only the warehouse reports a shipment, only for a paid order; shipping takes the stock out for good. The report arrives by one of two doors: a message from the warehouse service, or a gRPC call from a warehouse that has no system of its own. Both reach the same command. News about an order that has already left changes nothing. | `ShipOrderHandler`; `Api/Hosting/FulfillmentEventsConsumer.cs` (RabbitMQ); gRPC `Fulfillment.ShipOrder` (warehouse policy); `Order.Ship` |
| O10 | Every late or repeated answer is harmless. Stock reserved for a cancelled order is released, and money taken after cancellation is refunded. | `OrderProcessHandler` asks `CanConfirmStock`, `CanMarkPaid`, `CanCancel` first; `Order.RecordRefund` |
| O11 | An order has at least one line. | `OrderMustHaveLines`, checked in `Order.Place` |
| O13 | A paid order is announced twice, to two kinds of reader: `OrderPaid` to whoever wants to know (Kafka), and `OrderReadyToShip` to the warehouse (RabbitMQ), with the address and the lines and without any price. | `Order.MarkPaid`; `Domain/Events/OrderReadyToShip.cs` |
| O12 | No order waits for ever. When the request for its stock is given up, the order is cancelled `RESERVATION_FAILED`, its payment voided and its stock request released. | `Api/Hosting/GivenUpMessages.cs`, `StockReservationAbandoned`, `OrderProcessHandler` |

### Payments

| Code | Rule | Where |
|---|---|---|
| P1 | One payment per order; the order id is the provider's idempotency key. Registering it twice registers it once. | `Payment` keyed by the order, `PaymentMustBePending`; `RegisterPaymentHandler`; `DemoPayGateway` (`Idempotency-Key`) |
| P2 | Money is taken only after the stock is confirmed. | `OrderProcessHandler`: `StockReserved` → `AuthorizePayment` |
| P3 | A decline (HTTP 402) is a business answer and is never retried. | `AuthorizePaymentHandler` |
| P4 | An unavailable provider (5xx, timeout, open circuit) is retryable. The HTTP client retries first, then the message is redelivered with a cooldown, and finally it goes to the error queue. Meanwhile the order honestly stays "awaiting payment". | `DemoPayGateway`, `PaymentFailures.ProviderUnavailable`, `Program.cs` |
| P5 | The card token lives in the Payments module only: in the payment intent until an order uses it or it expires, then in the payment until the provider answers. No message between modules carries it, and it is never logged or audited. | `PaymentIntent.UseFor`; `PaymentIntentTokenEraser`; `Payment.MarkAuthorized`, `MarkDeclined`, `Void` erase the token; `ToString` of `CreatePaymentIntent`; the audit policy |
| P6 | A payment is for a positive amount. | `AmountMustBePositive`, checked in `Payment.Register` |
| P7 | Only a charged payment can be refunded. | `PaymentMustBeAuthorized`, checked in `Payment.MarkRefunded` |
| P8 | A payment intent pays for one order, of the shopper who created it, within thirty minutes. Only a provider token (`tok_...`) is accepted, never a card number. An intent that cannot pay when the charge is registered declines the payment at once. | `PaymentIntent`, `PaymentIntentMustBeUsable`; `CreatePaymentIntentValidator` (`PAYMENT_TOKEN_INVALID`); `RegisterPaymentHandler`, `Payment.Refuse` |

## 5. The order process

Every box is one transaction, and every transaction changes one module. The arrows between modules are
messages on the host's durable local queues; each leaves with the change that caused it (the
transactional outbox) and each receiver is idempotent by the order's identity.

```
customer       Basket              Ordering                 Payments            Catalog        DemoPay
  │ POST payments/intents (token) ───────────────────────────▶│ intent kept      │               │
  │◀──── 201 (paymentIntentId) ────────────────────────────────│                   │               │
  │ POST checkout │                    │                        │                   │               │
  │ + Idempotency-Key                  │                        │                   │               │
  │──────────────▶│ basket emptied     │                        │                   │               │
  │◀──── 202 ─────│── BasketCheckedOut ▶ order created,         │                   │               │
  │  (order id)   │                    │ Submitted              │                   │               │
  │               │                    │── RegisterPayment ────▶│ payment pending   │               │
  │               │                    │◀── PaymentRegistered ──│                   │               │
  │               │                    │── ReserveStock ───────────────────────────▶│ all or nothing│
  │               │                    │◀────────────────────────── StockReserved ──│               │
  │               │                    │ AwaitingPayment        │                   │               │
  │               │                    │── AuthorizePayment ───▶│── POST authorize ────────────────▶│
  │               │                    │                        │◀──── 201 ─────────────────────────│
  │               │                    │◀── PaymentAuthorized ──│                   │               │
  │               │                    │ Paid ── OrderPaid ────────▶ Kafka (the figures)            │
  │               │                    │      ── OrderReadyToShip ─▶ RabbitMQ (the warehouse)       │
```

The customer's answer is **202 Accepted**: the basket has been taken and the order has its identity, but
the order itself is created a moment later. `GET /v1/orders/{id}` answers 404 until then.

**Why messages, and what it costs.** Until this design, checkout was one Ordering transaction that also
emptied the basket and registered the payment, through interfaces the other modules published. It was
atomic, and it tied three modules to one database transaction. Vaughn Vernon's aggregate rule
(*Implementing Domain-Driven Design*) is one aggregate per transaction, with eventual consistency between
them; Kamil Grzybek's *Modular Monolith with DDD* lets modules integrate through events only; Microsoft's
eShop checks out by publishing a snapshot of the basket. The day a module becomes a service, a message
changes its transport and nothing else. The cost is the 202, and that a problem found after the Basket
has answered can only end in a cancelled order, never in a refused checkout. A call through Contracts
that writes remains a valid choice where two modules must change together and will stay in one
deployment. This sample does not show it.

**The payment token** is handed to Payments before checkout (`POST /v1/payments/intents`), and checkout
carries only the payment intent's identity. No message between modules holds a secret.

Alternative paths:

- **Out of stock:** Catalog answers `StockReservationRejected`; the order is cancelled `OUT_OF_STOCK` and
  the payment voided.
- **Card declined:** Payments answers `PaymentDeclined`; the order is cancelled `PAYMENT_DECLINED` and the
  stock released.
- **Stock request given up:** `ReserveStock` failed, was retried and went to the error queue; the host
  answers `StockReservationAbandoned`, and the order is cancelled `RESERVATION_FAILED` and the payment
  voided.
- **Provider down:** `AuthorizePayment` is retried with a cooldown and the order stays AwaitingPayment
  until the provider recovers or the customer cancels.

## 6. Scenarios

All of them run in [`scripts/scenarios.sh`](../scripts/scenarios.sh), and every step states its
expectation.

| ID | Story | What it shows of MP Core |
|---|---|---|
| S0 | Anybody browses the catalog without a token. | An explicit anonymous endpoint, the read-through cache, `SortAllowlist` |
| S1 | Sara buys a tent and two stoves. | One module per transaction, the outbox, durable local queues, the order process |
| S2 | Mina reprices the tent while Reza is shopping. | An integration event over Kafka, an idempotent consumer, the inbox, rollback of a failure |
| S3 | Mina types a price ten times too high. | A BusinessRule failure as Problem Details, audit of a refused attempt, a role policy |
| S4 | Sara orders five jackets; the warehouse has three. | A business answer instead of an exception, automatic cancellation |
| S5 | Reza's card has no money. | Compensation: the stock goes back |
| S6 | The provider answers 503 once. | The resilient HTTP client and the idempotency key |
| S7 | The provider is down, and Reza gives up and cancels. | Retry with cooldown, compensation, void |
| S8 | The last pairs of boots sell. | A domain event after commit, a restock alert |
| S9 | A warehouse without a system of its own ships over Commerce's gRPC door. | gRPC in a host that also serves REST, client credentials, stock taken out for good |
| S10 | Sara cancels a paid order. | Refund, order history |
| S11 | Who may see what. | 401, 403, 404 instead of 403, validation |
| S12 | The events on Kafka, with their partition keys. | Partitioning by order and by SKU |
| S13 | Ali translates a message; Reza reads it in Persian. | The message catalog, `Accept-Language`, stored translations, a business rule rendered with its arguments |
| S14 | Sara's checkout is sent twice; Mina enters the same delivery note twice. | Request idempotency (`Idempotency-Key`, `Idempotency-Replayed`, 400 and 422), and a business key where a request key is not enough |
| S15 | Reza's basket is checked out eight times at the same moment. | Optimistic concurrency, retry, and the outbox: a message leaves only with the change that caused it, so there is one order and one charge |
| S16 | Twelve orders for one product within a few seconds. | Optimistic concurrency under load, retry with growing pauses and jitter, and an answer for a message that is given up: every order comes to an end |
| S17 | Sara pays through a payment intent, then tries it again, then tries Reza's. | The card token stays with Payments: no message and no queue table holds it |
| S18 | The warehouse service ships Sara's order. | Two services and no shared code: outbox, RabbitMQ, inbox, a gRPC-only host, the answer on a second queue, a token's audience |
| S19 | Nora reads the figures of an order placed a moment ago. | A Kafka stream read into a TimescaleDB hypertable, a REST-only host, a cached report, a role policy, a validation message in Persian |
| S20 | Everything again, through the edge. | Apache APISIX in front of the three backends: TLS, REST and gRPC through one door, the request's identity, forwarded headers from a trusted proxy, a forged identity header, a rate limit |
| S21 | The edge verifies tokens as well, when it is switched on. | Two walls: what a gateway can know about a token (its signature) and what only a backend knows (its audience, its holder's role). Skipped, with the reason, when the switch is off |

## 7. API

### Commerce, REST (`http://localhost:5100`, Swagger at `/openapi-ui/`)

| Method and path | Role | Does |
|---|---|---|
| `GET /v1/catalog/products` | anonymous | Browse: `category`, `search`, `page`, `size`, `sort` (`name`, `price`, `newest`), `desc` |
| `GET /v1/catalog/products/{sku}` | anonymous | Product page (cached) |
| `POST /v1/catalog/products` | catalog-manager | List a product |
| `PUT /v1/catalog/products/{sku}/price` | catalog-manager | Change a price, with a reason |
| `POST /v1/catalog/products/{sku}/restock` | catalog-manager | Receive a delivery: `quantity`, `reference` (the delivery note). `Idempotency-Key` optional |
| `POST /v1/catalog/products/{sku}/discontinue` | catalog-manager | Withdraw a product |
| `GET /v1/catalog/products/{sku}/stock` | catalog-manager | Warehouse numbers |
| `GET /v1/catalog/restock-alerts` | catalog-manager | Restock alerts |
| `GET /v1/basket` | customer | My basket. A read: it changes nothing |
| `POST /v1/basket/acknowledge-prices` | customer | I have seen the changed prices |
| `PUT /v1/basket/items/{sku}` | customer | Set a quantity (0 removes) |
| `DELETE /v1/basket/items/{sku}` | customer | Remove a line |
| `POST /v1/payments/intents` | customer | Hand over the provider's `paymentToken`; answers 201 with `paymentIntentId` and `expiresOnUtc`. `Idempotency-Key` optional |
| `POST /v1/basket/checkout` | customer | Check out with `shippingAddress`, `paymentIntentId`, `expectedTotal`. `Idempotency-Key` required. Answers 202 with `orderId` and `Location` |
| `GET /v1/orders` | customer | My orders |
| `GET /v1/orders/{id}` | the buyer, support, warehouse | Details and history. 404 for the moment between checkout and the order's creation |
| `POST /v1/orders/{id}/cancel` | the buyer, support | Cancel |
| `GET /v1/backoffice/orders` | support-agent | Every order, filter `status` |
| `GET /v1/backoffice/audit` | support-agent | Audit trail: `module`, `entityType`, `entityId` |
| `GET /v1/backoffice/translations?culture=fa` | support-agent | Stored message translations |
| `PUT /v1/backoffice/translations/{culture}/{key}` | support-agent | Store or change a message text for one language |
| `DELETE /v1/backoffice/translations/{culture}/{key}` | support-agent | Remove it; the resource file's text applies again |

### Commerce, gRPC (`localhost:5101`, package `storefront.commerce.v1`)

| Service and method | Role |
|---|---|
| `Fulfillment/ListOrdersToShip` | warehouse |
| `Fulfillment/GetOrder` | warehouse |
| `Fulfillment/ShipOrder` | warehouse |
| `Catalog/GetProduct` | any authenticated service |

### Messages between the backends

| Channel | Broker | Key | Published by | Read by |
|---|---|---|---|---|
| `storefront.catalog.product-price-changed.v1` | Kafka | SKU | Commerce (Catalog) | Commerce (Basket) |
| `storefront.ordering.order-placed.v1` | Kafka | order id | Commerce (Ordering) | Analytics |
| `storefront.ordering.order-paid.v1` | Kafka | order id | Commerce (Ordering) | Analytics |
| `storefront.ordering.order-cancelled.v1` | Kafka | order id | Commerce (Ordering) | Analytics |
| `storefront.ordering.order-shipped.v1` | Kafka | order id | Commerce (Ordering) | nobody yet |
| `storefront.fulfillment.orders-ready-to-ship.v1` | RabbitMQ | | Commerce (Ordering) | Fulfillment |
| `storefront.commerce.shipments-dispatched.v1` | RabbitMQ | | Fulfillment | Commerce |

## 8. Where things are in Commerce

```
commerce/src/
  Storefront.Commerce.Api/
    Program.cs                        host composition: security, modules, message catalog, validators, Kafka routes, retry policy
    Hosting/StorefrontPolicies.cs         role policies
    Hosting/CatalogEventsConsumer.cs  consumes ProductPriceChanged from Kafka for the Basket
    Hosting/FulfillmentEventsConsumer.cs  consumes ShipmentDispatched from RabbitMQ for Ordering
    Hosting/CommerceTopics.cs         Kafka topic names
    Hosting/CommerceQueues.cs         RabbitMQ queue names
    Hosting/HostHealthChecks.cs       what "alive" and "ready" ask
    Hosting/DevelopmentSetup.cs       migrations and catalog seed in Development
    Hosting/GivenUpMessages.cs        what the order process is told when a step ends in the error queue
    Hosting/TranslationHandlers.cs    support edits message texts (commands, validators, failures)
    Resources/HostMessages*.resx      the host's own message texts, English and Persian
    Rest/Endpoints/StorefrontEndpoints.cs   every REST endpoint
    Grpc/Services/CommerceGrpcServices.cs   the gRPC services
    Protos/storefront_commerce.proto      the gRPC contract
  Storefront.Commerce.Infrastructure/
    DependencyInjection.cs            hybrid cache, DbContext, audit, idempotency, stored translations, module registration
    Persistence/AppDbContext.cs       the host's only unit of work; applies every module's mappings
    Audit/AuditPolicyConfiguration.cs what is audited
    Migrations/                       generated with dotnet ef
  Modules/<Context>/Storefront.Commerce.Modules.<Context>/      one project per bounded context
    Domain/                           aggregates, child entities, value objects, Events/, Rules/ (one class per rule)
    Application/Commands/             one file per command: the record and its handler
    Application/Queries/              one file per query
    Application/Views/                what queries and commands return
    Application/Ports/                the interfaces the module needs: repositories, read model, gateway
    Application/Process/              OrderProcessHandler (Ordering): from a checked-out basket to a paid order
    Application/Events/               reactions to the module's own domain events (Catalog)
    Application/Validators/           FluentValidation validators, one per caller-facing command
    Infrastructure/                   EF mappings, repositories, read models, the DemoPay gateway, AddXModule
    Resources/<Context>Messages.resx  message texts, English; <Context>Messages.fa.resx, Persian
  Modules/<Context>/Storefront.Commerce.Modules.<Context>.Contracts/  what other modules may use (Catalog, Basket, Payments):
                                      module messages, and one read-only interface, ICatalogLookup
commerce/tests/Storefront.Commerce.Tests/
    Unit/          rules and value objects, handlers, validators, message catalog, the checkout contract between Basket and Ordering
    Architecture/  module and layer boundaries, from the compiled assemblies (NetArchTest)
    Component/     the EF model, the schemas and the keys that stay inside them, value objects as columns, and migrations matching the model
```

## 9. Fulfillment: the warehouse

One aggregate, `Shipment`, keyed by the order it ships. The warehouse knows what to pick and where to
send it. It does not know what anything costs or how it was paid.

| Code | Rule | Where |
|---|---|---|
| F1 | A shipment is dispatched once. A second report for the same parcel is refused. | `ShipmentMustBePending`, checked in `Shipment.Dispatch` |
| F2 | There is something to ship: a shipment has at least one line. | `ShipmentMustHaveLines`, checked in `Shipment.Receive` |
| F3 | An order is received once, however often the message arrives. | the inbox, and `Shipment` keyed by the order; `OrderReadyToShipHandler` |
| F4 | Dispatching names the carrier and the tracking code, and tells the shop. | `DispatchShipmentValidator`; `Shipment.Dispatch` raises `ShipmentDispatched`, which leaves through the outbox |
| F5 | Dispatching is audited, with the carrier and the tracking code; the recipient's name, phone and address are not. | `Infrastructure/Audit/AuditPolicyConfiguration.cs` |

gRPC (`localhost:5201`, package `storefront.fulfillment.v1`), role `warehouse`:

| Service and method | Does |
|---|---|
| `Shipments/ListShipments` | What is waiting, or what has left: `status`, `page`, `size` |
| `Shipments/GetShipment` | One shipment, by the order's identity |
| `Shipments/DispatchShipment` | The parcel has left: `carrier`, `tracking_code` |

```
fulfillment/src/
  Storefront.Fulfillment.Domain/           Shipment, ShipmentLine, DeliveryAddress, Rules/, Events/ShipmentDispatched
  Storefront.Fulfillment.Application/
    Contracts/OrderReadyToShip.cs          this service's copy of what Commerce sends
    Events/OrderReadyToShipHandler.cs      an order arrives
    Commands/DispatchShipment.cs           a parcel leaves
    Queries/                               GetShipment, ListShipments
  Storefront.Fulfillment.Infrastructure/   the EF model (schema fulfillment), audit policy, migrations
  Storefront.Fulfillment.Api/
    Protos/storefront_fulfillment.proto    the gRPC contract
    Grpc/Services/ShipmentsService.cs
    Hosting/FulfillmentQueues.cs           the two queues
    Program.cs                             the listener, the route, the retry rules
```

## 10. Analytics: the figures

One aggregate, `OrderFact`: one thing that happened to one order, at one moment. Facts are only ever
added. They live in a TimescaleDB hypertable, cut into one chunk per day.

| Code | Rule | Where |
|---|---|---|
| A1 | A fact is recorded once: its identity is the identity of the event that reported it. | `OrderFact`, keyed by the event; the inbox; `OrderEventsHandler` |
| A2 | A report covers at most 31 days, and its end is after its start. | `SalesReportsHandler.Period`; `AnalyticsFailures` (`PERIOD_TOO_LONG`, `PERIOD_INVALID`) |
| A3 | Without a period, the hourly report covers the last 24 hours and the regional report the last 7 days. | `SalesReportsHandler` |
| A4 | A report is counted in whole minutes and may be up to five seconds old. | `SalesReportsHandler.CacheKey`, `ReportLifetime` |
| A5 | Revenue is what was paid; an order that was placed and not paid counts as placed only. | `Infrastructure/Persistence/SalesReadModel.cs` |

REST (`http://localhost:5300`), role `analyst`:

| Method and path | Does |
|---|---|
| `GET /v1/analytics/sales/hourly` | Paid orders and revenue per hour: `from`, `to` |
| `GET /v1/analytics/sales/by-region` | Orders placed per region and city: `from`, `to` |

```
analytics/src/
  Storefront.Analytics.Domain/OrderFact.cs
  Storefront.Analytics.Application/
    Contracts/OrderEvents.cs               this service's copies of the three order events
    Events/OrderEventsHandler.cs           an event becomes a fact
    Queries/SalesReports.cs                the two reports
  Storefront.Analytics.Infrastructure/
    Persistence/SalesReadModel.cs          time_bucket over the hypertable
    Migrations/                            the hypertable is created in the first migration
  Storefront.Analytics.Api/
    Rest/Endpoints/SalesEndpoints.cs
    Hosting/AnalyticsTopics.cs             the three topics and the consumer group
    Program.cs                             the listeners
```
