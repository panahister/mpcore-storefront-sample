# Running Storefront

## What you need

| What | Why |
|---|---|
| .NET SDK `10.0.400` | pinned in `global.json` |
| Docker, with about 8 GB of memory | every dependency runs in a container; the three backends do not |
| `curl`, `jq`, `uuidgen` | the scenarios |
| `grpcurl` | scenarios S9 and S18; without it they are skipped |
| On a Mac with Apple Silicon: `brew install protobuf grpc`, or Rosetta | the gRPC code generator that ships with .NET is built for Intel |

## Start

```bash
scripts/up.sh
```

Starts PostgreSQL, TimescaleDB, Kafka, RabbitMQ, Redis, Keycloak, a simulated payment provider (WireMock),
and the OpenTelemetry Collector with Jaeger, Prometheus and Grafana. The first start downloads the images.
`scripts/up.sh --no-observability` leaves the last four out.

```bash
scripts/setup.sh
```

Once. Writes the addresses of the dependencies into each backend's user secrets, outside the repository.
Run it again when you change a port in `infrastructure/.env`.

```bash
scripts/run.sh commerce
```

```bash
scripts/run.sh fulfillment
```

```bash
scripts/run.sh analytics
```

Each in a terminal of its own, or from your IDE with the environment `Development`. On its first start a
backend creates its tables; Commerce also lists twelve products.

```bash
scripts/scenarios.sh
```

Every scenario, or some of them: `scripts/scenarios.sh S1 S18`. S9 ships the order S1 placed, so name S1
with it.

## Stop

```bash
scripts/down.sh
```

Stops the containers and keeps the data. To delete the data as well and start clean:

```bash
scripts/down.sh --volumes
```

## What you can open

| What | Address | Sign-in |
|---|---|---|
| Commerce, its API described | http://localhost:5100/openapi-ui/ | none, in Development |
| Analytics, its API described | http://localhost:5300/openapi-ui/ | none, in Development |
| Keycloak | http://localhost:48180 | `admin` / `admin`, realm `storefront` |
| Kafka, in a browser | http://localhost:48080 | none |
| RabbitMQ | http://localhost:45673 | `storefront` / `storefront` |
| Jaeger | http://localhost:46686 | none |
| Grafana | http://localhost:43000 | `admin` / `admin`, dashboard "Storefront Commerce" |
| Prometheus | http://localhost:49090 | none |
| The payment provider's request log | http://localhost:48081/__admin/requests | none |
| PostgreSQL | `localhost:45432`, databases `storefront_commerce` and `storefront_fulfillment` | `storefront` / `storefront` |
| TimescaleDB | `localhost:45433`, database `storefront_analytics` | `storefront` / `storefront` |

These names and passwords exist for containers on your machine. None of them is a secret, and none of
them belongs anywhere else.

## The people

| User | Password | Role |
|---|---|---|
| `sara`, `reza` | `sara-lab`, `reza-lab` | `customer` |
| `mina` | `mina-lab` | `catalog-manager` |
| `ali` | `ali-lab` | `support-agent` |
| `nora` | `nora-lab` | `analyst` |
| client `storefront-warehouse` | secret `lab-only-warehouse-secret` | `warehouse` |

## By hand

A token:

```bash
TOKEN=$(curl -s -X POST http://localhost:48180/realms/storefront/protocol/openid-connect/token -d grant_type=password -d client_id=storefront-web -d username=sara -d password=sara-lab | jq -r .access_token)
```

Something in the basket:

```bash
curl -s -X PUT http://localhost:5100/v1/basket/items/TNT-ALV-2P -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"quantity":1}' | jq
```

Paying takes two calls. First the provider's token goes to Payments, which answers a payment intent:

```bash
INTENT=$(curl -s -X POST http://localhost:5100/v1/payments/intents -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"paymentToken":"tok_visa_ok"}' | jq -r .paymentIntentId)
```

Then checkout names the intent and the total you saw. It requires an `Idempotency-Key`: any unique text
of up to 255 characters. It answers 202 with the order's identity, and the order can be read a moment
later.

```bash
curl -s -X POST http://localhost:5100/v1/basket/checkout -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(uuidgen)" -H 'Content-Type: application/json' -d "{\"shippingAddress\":{\"recipientName\":\"Sara Ahmadi\",\"phone\":\"+14155550123\",\"province\":\"California\",\"city\":\"San Francisco\",\"line\":\"12 Harbour Street\",\"postalCode\":\"94103\"},\"paymentIntentId\":\"$INTENT\",\"expectedTotal\":485.00}" | jq
```

The payment token chooses what the provider does:

| Token | The provider |
|---|---|
| `tok_visa_ok` | approves |
| `tok_insufficient_funds`, `tok_card_expired` | declines |
| `tok_flaky` | answers 503 once, then approves |
| `tok_psp_down` | always answers 503 |

Add `Accept-Language: fa` to any request to read its messages in Persian.

## Common problems

| What you see | Why, and what to do |
|---|---|
| `Bad CPU type in executable` while building on a Mac | See "What you need", the last row. |
| `fail: ... __EFMigrationsHistory` in the log of a first start | Harmless. EF Core reads the history table before it creates it. |
| A backend fails at startup with "relation already exists" | The database was created by an older first migration. This sample has one migration per backend, written again when the model changes. Start clean: `scripts/down.sh --volumes`, then `scripts/up.sh`. |
| A port is taken | Change it in `infrastructure/.env`, then run `scripts/up.sh` and `scripts/setup.sh` again. |
| A change to the realm is not picked up | Keycloak imports the realm into an empty database only. Start clean. |
| Scenario S9 is skipped | `grpcurl` is not installed, or S1 did not run before it. |
| Scenarios S18 or S19 are skipped | Fulfillment or Analytics is not running. |
| Checkout answers 400 `KEY_REQUIRED` | The request has no `Idempotency-Key` header. |
| A token is refused with 401 by one backend and accepted by another | A token names the backends it is for. The web client's token is for Commerce and Analytics, the warehouse's for Commerce and Fulfillment. |
