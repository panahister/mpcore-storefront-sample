# Storefront: the MP Core sample

[![ci](https://github.com/panahister/mpcore-storefront-sample/actions/workflows/ci.yml/badge.svg)](https://github.com/panahister/mpcore-storefront-sample/actions/workflows/ci.yml)
[![licence](https://img.shields.io/badge/licence-Apache--2.0-blue)](LICENSE)
[![MP Core](https://img.shields.io/badge/MP%20Core-0.9.0-512bd4)](https://github.com/panahister/mpcore)

An online store built with [MP Core](https://github.com/panahister/mpcore), as three backends that work together.
It exists to show a developer, in running code, how a backend is built on MP Core: where a business rule
lives, how a module tells another module that something happened, what happens when a request or a message
arrives twice, and what a failure looks like to the caller.

Everything here runs on your machine. Nothing is a mock-up: the backends take real tokens from Keycloak,
write to PostgreSQL and TimescaleDB, and talk over Kafka, RabbitMQ, REST and gRPC. Only the payment
provider is simulated.

## The system

```text
                        shopper, back office                      warehouse staff            analyst
                               │ REST                                   │ gRPC                  │ REST
                               ▼                                        ▼                       ▼
┌────────────────────────────────────────────────┐            ┌──────────────────┐    ┌──────────────────┐
│ Commerce            modular monolith           │            │ Fulfillment      │    │ Analytics        │
│ REST :5100  gRPC :5101                         │  RabbitMQ  │ service          │    │ service          │
│                                                │───────────▶│ gRPC :5201       │    │ REST :5300       │
│  Catalog ─ Basket ─ Ordering ─ Payments        │ order ready│                  │    │                  │
│  four modules, one database, a schema each     │◀───────────│ shipments        │    │ sales per hour   │
│                                                │  shipment  │                  │    │ and per city     │
│                                                │ dispatched └──────────────────┘    └──────────────────┘
│                                                │                     │                       ▲
│                                                │─── Kafka: order placed, paid, cancelled ────┘
└────────────────────────────────────────────────┘                     │
        │            │            │                                    │
   PostgreSQL      Redis       DemoPay (simulated)                PostgreSQL              TimescaleDB
```

| Backend | Shape | Transport | Messaging | Storage | Also |
|---|---|---|---|---|---|
| [`commerce/`](commerce) | modular monolith, four modules | REST and gRPC | Kafka and RabbitMQ | PostgreSQL | hybrid cache (memory and Redis), business audit, request idempotency, inbox, stored translations |
| [`fulfillment/`](fulfillment) | service | gRPC | RabbitMQ | PostgreSQL | business audit, inbox |
| [`analytics/`](analytics) | service | REST | Kafka | TimescaleDB hypertable | memory cache, inbox |

The three share no code. Each declares the messages it reads in its own project, and a test holds the
copies together ([docs/architecture.md](docs/architecture.md), "Contracts between services").

## Run it

You need the .NET SDK `10.0.400`, Docker with about 8 GB of memory, `curl` and `jq`. `grpcurl` is needed
for the two gRPC scenarios. [docs/running.md](docs/running.md) has the details, the addresses you can open
and the problems people meet.

```bash
git clone https://github.com/panahister/mpcore-storefront-sample.git
```

```bash
cd mpcore-storefront-sample
```

```bash
scripts/up.sh
```

```bash
scripts/setup.sh
```

Then each backend in a terminal of its own:

```bash
scripts/run.sh commerce
```

```bash
scripts/run.sh fulfillment
```

```bash
scripts/run.sh analytics
```

And the twenty business scenarios, against the running backends:

```bash
scripts/scenarios.sh
```

Every step prints what it expects and what it got. The script is a demonstration and an end-to-end test at
the same time: it exits non-zero when an expectation fails.

## Learn from it

| You want to | Read |
|---|---|
| Follow a path through the code, one idea at a time | [docs/learning-path.md](docs/learning-path.md) |
| Know the business: roles, rules, scenarios, API | [docs/business.md](docs/business.md) |
| Know why the system is cut this way, and what each decision costs | [docs/architecture.md](docs/architecture.md) |
| Find where a capability of MP Core is used | [docs/mpcore-coverage.md](docs/mpcore-coverage.md) |
| Know what building this taught, including what went wrong | [docs/lessons.md](docs/lessons.md) |
| Work here with an AI coding agent | [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md), and the skills in each backend's `.mpcore/skills` |
| Start a backend of your own | [MP Core: getting started](https://github.com/panahister/mpcore/blob/main/docs/guide/getting-started.md) |

Each backend also carries the guide the MP Core template generated for it (`README.md` and `docs/` inside
`commerce/`, `fulfillment/` and `analytics/`). Those describe a generated backend in general; the documents
above describe this system.

## Tests

| What | Command | Needs |
|---|---|---|
| Commerce: rules, handlers, validators, messages, architecture, the EF model | `dotnet test commerce/Storefront.Commerce.Backend.sln` | nothing |
| Fulfillment | `dotnet test fulfillment/Storefront.Fulfillment.Backend.sln` | nothing |
| Analytics | `dotnet test analytics/Storefront.Analytics.Backend.sln` | nothing |
| The contracts between the three | `dotnet test tests/Storefront.Contracts.Tests` | nothing |
| Every business scenario, end to end | `scripts/scenarios.sh` | the dependencies and the three backends |
| All of it against packed MP Core packages | `scripts/verify-against-packages.sh` | a clone of MP Core next to this repository |

## Where MP Core comes from

From nuget.org, version `0.9.0`. When a clone of MP Core sits next to this repository (`../mpcore`), the
backends build against its source instead: a breakpoint in framework code works, and a change there is
picked up by the next build. [`Directory.Build.targets`](Directory.Build.targets) decides, and says so in
the build output. To choose for one build:

```bash
dotnet build commerce/Storefront.Commerce.Backend.sln -p:MPCoreSource=NuGet
```

## Licence

Apache-2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE). Storefront, DemoPay and the people in the
scenarios are fictional.
