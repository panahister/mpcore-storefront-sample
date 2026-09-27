# Storefront: instructions for Claude Code

This repository is the sample for MP Core: three backends that work together. Read this file first, then
the instructions of the backend you are about to change.

## The layout

| Folder | What it is | Its own instructions |
|---|---|---|
| `commerce/` | modular monolith: Catalog, Basket, Ordering, Payments. REST and gRPC, Kafka and RabbitMQ | `commerce/AGENTS.md`, `commerce/CLAUDE.md` |
| `fulfillment/` | service: the warehouse. gRPC, RabbitMQ | `fulfillment/AGENTS.md`, `fulfillment/CLAUDE.md` |
| `analytics/` | service: the sales figures. REST, Kafka, TimescaleDB | `analytics/AGENTS.md`, `analytics/CLAUDE.md` |
| `tests/Storefront.Contracts.Tests/` | what holds the messages between the three together | |
| `scripts/`, `infrastructure/` | the dependencies, and the scenarios that run against the backends | |
| `docs/` | the business, the architecture, what MP Core contributes, what was learned | |

Each backend is a repository of its own in everything but location. It has its own
`.mpcore/template-manifest.json`, its own skills in `.mpcore/skills`, and its own guides in `docs/`.

## Skills

| Where you were started | The skills you find | What they are |
|---|---|---|
| At the root of this repository | ten, named `storefront-...`, in `.claude/skills` | Each finds the backend a task belongs to and hands over to that backend's skill |
| In a backend's folder | ten, named `mpcore-...`, in `<backend>/.claude/skills` | MP Core's skills, written for that backend's shape, transport and broker |

Either way the procedure is in one place: `<backend>/.mpcore/skills/<name>/SKILL.md`. Read it before
you act. [docs/building-with-ai-agents.md](docs/building-with-ai-agents.md) shows each skill at work on
this repository.

## Start every task this way

1. **Decide which backend the task belongs to**, and read that backend's instructions and manifest. A
   task that needs two backends is two changes and a contract between them.
2. **Read the rule before the code.** [docs/business.md](docs/business.md) lists every business rule by its
   code and the place it lives. A rule that is not there does not exist: ask, do not invent.
3. **Select the one skill that fits**, and read its body in the backend's `.mpcore/skills`. [docs/learning-path.md](docs/learning-path.md) names the skill for each kind of work, with a
   worked example of it in this repository.
4. **Change one thing**, in the backend and the module that owns it.
5. **Prove it.** Run the backend's tests, and the scenarios that touch what you changed
   (`scripts/scenarios.sh S1 S18`). Report the real output, failures included.

## Rules of this repository

- **The three backends share no code.** Never add a project reference from one backend to another, and
  never a shared contracts library. A reader declares its own copy of a message, and
  `tests/Storefront.Contracts.Tests` holds the copies together. That test project is the only one that
  may reference more than one backend.
- **A module writes only its own data.** In Commerce, another module is told by a message. A handler takes
  repositories of its own module only. The architecture tests enforce both.
- **A defect of MP Core is not worked around here.** Say what you found, with the smallest case that
  shows it, and stop. MP Core is fixed in its own repository.
- **A handler never calls `SaveChangesAsync`.** MP Core saves, and commits the messages with the change.
- **A query only reads.** It declares no unit of work, publishes nothing, and is the only thing a `GET`
  sends.
- **A message that carries something personal or secret overrides `ToString`.** A failed message is
  printed into the log.
- **A guarantee is proved by a test that was seen failing, or by an experiment against the running
  system.** Do not write that something is guaranteed because a document says so.
- **Documentation is in English**, and a convention names its source.
- **Nothing is committed, pushed or published without the owner's word.**

## Commands

| To | Run |
|---|---|
| start the dependencies | `scripts/up.sh` |
| write local settings, once | `scripts/setup.sh` |
| run a backend | `scripts/run.sh commerce` (or `fulfillment`, `analytics`) |
| test a backend | `dotnet test commerce/Storefront.Commerce.Backend.sln` |
| test the contracts | `dotnet test tests/Storefront.Contracts.Tests` |
| run the scenarios | `scripts/scenarios.sh`, or some: `scripts/scenarios.sh S1 S9` |
| add a migration | build first, then `dotnet ef migrations add <Name> --project <backend>/src/<...>.Infrastructure --startup-project <backend>/src/<...>.Api`, with `ConnectionStrings__PostgreSql` set; then build again |
| test against packed MP Core | `scripts/verify-against-packages.sh` |
