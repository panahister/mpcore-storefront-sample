# Building with AI coding agents

How to build on MP Core with Claude Code or with Codex, shown on Storefront. The first part is a record
of a real run: one task, given to both agents, and what each did. The second part takes the ten skills one
by one, with what you would type and where this repository shows the result.

Nothing here is a promise about a model. A model changes every few months; what this document shows is
that an agent which is **given the procedure** keeps the architecture, and what a person still has to
check.

## 1. Why skills

An agent that writes a handler which calls `SaveChanges`, or an endpoint that reads the user from a
header, has written code that compiles, passes review on a busy day, and breaks a guarantee. An agent
keeps a convention it can see, and cannot keep one that lives in the heads of a team.

A **skill** is the convention, written as a procedure: what to establish before writing, what to ask
the owner, what must never be decided alone, and how to prove the result. It is a file in the
repository. A person can read it, review a change to it, and hold the agent to it.

## 2. Where the skills are, and how each agent finds them

| You start the agent | Claude Code finds | Codex finds |
|---|---|---|
| at the root of this repository | ten skills, `storefront-...`, in `.claude/skills` | ten skills, `storefront-...`, in `.agents/skills` |
| in a backend's folder, such as `commerce/` | ten skills, `mpcore-...`, in `commerce/.claude/skills` | ten skills, `mpcore-...`, in `commerce/.agents/skills` |

The procedure itself is in one place, whichever way it is reached:
`<backend>/.mpcore/skills/mpcore-<name>/SKILL.md`. What the agents discover are short files that lead
there, so that there is one body to keep.

- **In a backend of your own**, which MP Core generates, there is one backend and its ten `mpcore-...`
  skills. Start the agent at its root.
- **In this repository** there are three backends. A `storefront-...` skill at the root finds the backend
  a task belongs to, and hands over to that backend's `mpcore-...` skill.

**This was checked, not assumed.** Both agents were started in both places and asked which skills they
see:

| Started | Claude Code 2.1.281 | Codex CLI 0.155 |
|---|---|---|
| in `commerce/` | the ten `mpcore-...` skills | the ten `mpcore-...` skills |
| at the root, **before** the root had skills of its own | none | none |
| at the root, as it is now | the ten `storefront-...` skills | the ten `storefront-...` skills |

The middle row is a defect this check found. Both agents look for skills from the folder they are
started in; neither looks into the folders below it at the start. A repository with several backends
needs skills at its root, and this one had none. Both agents were then given a task for the warehouse
("put a shipment on hold") and asked only where they would read their instructions: both named
`fulfillment/.mpcore/skills/mpcore-implement-vertical-slice/SKILL.md` and quoted its first line.

## 3. A run, as it happened

On 2026-09-27 one task was given to both agents, each in a copy of this repository of its own, each
without a person answering questions. The task, the two answers and the two changes are kept as they
were, in [`docs/agent-runs/2026-09-27`](agent-runs/2026-09-27).

### The task

> Use the skill storefront-implement-vertical-slice.
>
> The owner has approved one capability, in the Analytics backend only: an analyst reads how many orders
> were cancelled, per reason.

followed by six acceptance criteria, what to deliver, and what not to touch:
[`task.md`](agent-runs/2026-09-27/task.md). It is written the way the skill asks for a task to be
written: a capability that was approved, criteria that can be checked, and a boundary.

### What each agent did

| | Claude Code | Codex |
|---|---|---|
| Found the skill, and through it the Analytics backend's procedure | yes | yes |
| Files changed | 7 | 7 |
| The same seven files, and nothing outside the boundary | yes | yes |
| The query, its handler, the view, the read model's SQL, the endpoint | yes | yes |
| Rule A6 and the endpoint, in `docs/business.md` | yes | yes |
| Tests it added | 7 | 2 |
| Built with warnings as errors | 0 warnings, 0 errors | 0 warnings, 0 errors |
| Ran the tests itself | yes: 19 of 19 | no: its sandbox refused the sockets the test runner needs, **and it said so** |
| Tests, run afterwards by a person | 19 of 19 | 14 of 14 |
| The change | [`claude-code.patch`](agent-runs/2026-09-27/claude-code.patch) | [`codex.patch`](agent-runs/2026-09-27/codex.patch) |
| Its own report | [`claude-code-answer.md`](agent-runs/2026-09-27/claude-code-answer.md) | [`codex-answer.md`](agent-runs/2026-09-27/codex-answer.md) |

Both kept what the architecture asks for, without being reminded in the task: a query that declares no
unit of work and reads through the read model; the SQL in the adapter and nowhere else, with every value
a parameter; the period and the cache shared with the two reports that existed; the role from the group
of endpoints; no sentence written in code.

### What a person checked afterwards

An agent's report is a claim. These are the checks that made it a fact:

| Check | Claude Code's change | Codex's change |
|---|---|---|
| The tests of the Analytics backend | 19 passed | 14 passed |
| The backend, started against the live database; the report asked for as an analyst | 200, three reasons | 200, the same three reasons |
| The rows, compared with a query typed into the database by hand | the same | the same |
| As a customer; without a token; with a period of five months | 403; 401; 400 | 403; 401; 400 |

### What each agent was unsure about, in its own words

Both did what the skill asks at the end: say what was not proved.

- **Claude Code** reported that the SQL had never run against a database, because it was not allowed to
  look for one; that "a reason that is missing" could mean null only, where it had chosen null, empty
  and blank; and that it had put the query next to the other two reports and not in a file of its own.
- **Codex** reported that no test had run in its sandbox and that it had therefore **no pass count to
  report**; and that it had read "missing" as null.

Neither invented a number. That sentence is the reason this document exists.

### What was merged

Claude Code's change, because it came with more tests. Scenario S19 was then extended by hand: an order
is cancelled, and the report is read until it shows one more. Codex's change is kept next to it and was
not merged; it is correct, and a repository needs one.

### What this run does not show

- **It is one task and one run each.** It is not a comparison of two products. The agents ran with
  different models and settings, and the times they took (about two and a half minutes, and about eight)
  say nothing general.
- **Nobody answered questions.** The skills tell an agent to ask when a requirement is unclear. Here the
  task left little unclear, on purpose, and an agent that cannot ask has to guess or to say so. Both said
  so.
- **The task was small.** One query in the simplest of the three backends.

## 4. How Storefront was built

Storefront and MP Core's skills were written by a person and AI coding agents working together: the
person decided, asked, doubted and refused; the agents wrote, ran and measured. The skills are what that
work taught, written down so that the next agent does not have to be told again.
[lessons.md](lessons.md) tells what went wrong on the way, including mistakes in the design that a
person caught in review.

## 5. The ten skills, on Storefront

For each skill: when to use it, what to type, and where this repository shows what the result looks
like. What you type is the same for both agents. Paths that begin with a module's name are in Commerce,
inside `commerce/src/Modules/<Module>/Storefront.Commerce.Modules.<Module>/`.

### Plan a bounded context

| | |
|---|---|
| When | Before any code: a new context, a new module, or a capability nobody has cut into commands yet |
| Type | *Use the skill storefront-plan-bounded-context. The warehouse wants to give a parcel back to the shop when the carrier could not deliver it. Approved so far: a returned parcel puts the stock back. Plan it; list what is unclear and ask me before you propose anything.* |
| The agent | names the aggregate and its rules, each with a code; the commands and queries; what the other backends are told, and what they are never told. It lists what it refuses to guess |
| In this repository | [business.md](business.md), section 9: the plan of the warehouse |

### Create a module

| | |
|---|---|
| When | A bounded context gets its place in the code |
| Type | *Use the skill storefront-implement-ddd-module. Create the module Reviews in Commerce, following the plan in docs/business.md. The skeleton only, no business yet. Build, and report the real result.* |
| The agent | makes one project with Domain, Application and Infrastructure folders and a Contracts project next to it, registers it, and names it for handler discovery |
| In this repository | `commerce/src/Modules/Payments`, and `commerce/src/Storefront.Commerce.Api/Hosting/HandlerAssemblies.cs` |

### Implement one capability, end to end

| | |
|---|---|
| When | One capability that was approved, with criteria that can be checked |
| Type | the task of section 3 |
| The agent | writes the rule, the handler, the validator, the mapping, the endpoint with its policy, and the tests |
| In this repository | Changing a price: `Catalog/Domain/Rules/PriceMoveMustBeGradual.cs`, `Catalog/Application/Commands/ChangeProductPrice.cs`, scenario S3. And the report of section 3 |

### Design a contract

| | |
|---|---|
| When | A caller outside the backend is going to depend on what you publish |
| Type | *Use the skill storefront-design-transport-contract. The warehouse's system will ask which shipments wait longer than a day. Design the gRPC method; say what a later change would break.* |
| The agent | uses the transport the backend has, keeps the failure model, and says which changes are breaking |
| In this repository | `fulfillment/src/Storefront.Fulfillment.Api/Protos/storefront_fulfillment.proto`; the REST API in [business.md](business.md), section 7 |

### Apply security

| | |
|---|---|
| When | Who may do this, and on whose record |
| Type | *Use the skill storefront-apply-security. Only support may read another shopper's order. A shopper who asks for one must learn nothing, not even that it exists. Add the tests for no token, the wrong role, and another shopper's order.* |
| The agent | adds a named policy at the endpoint, puts ownership into the application layer, and reads the actor from the token only |
| In this repository | `Ordering/Application/OrderAccess.cs`; `commerce/src/Storefront.Commerce.Api/Hosting/StorefrontPolicies.cs`; scenarios S11 and S21 |

### Apply business audit

| | |
|---|---|
| When | Somebody will ask, in a year, who did this |
| Type | *Use the skill storefront-apply-business-audit. A change of price is audited, and so is a change that was refused. The reason the manager gave is kept. Ask me which fields matter before you include any.* |
| The agent | names the fields in the audit policy, records the action in the handler, records a refused attempt apart, and keeps secrets and personal data out |
| In this repository | `Catalog/Application/Commands/ChangeProductPrice.cs`; `commerce/src/Storefront.Commerce.Infrastructure/Audit/AuditPolicyConfiguration.cs`; scenario S3 |

### Apply observability

| | |
|---|---|
| When | An operator has to see it, or act on it |
| Type | *Use the skill storefront-apply-observability. Count the orders by the status they reach. Confirm that nothing personal reaches a log, a label or a printed message.* |
| The agent | adds the metric an operator would act on, and checks what a failed message prints |
| In this repository | `Ordering/Application/OrderingMetrics.cs`; the test `A_message_never_prints_its_secret`; `Hosting/HostHealthChecks.cs` |

### Configure messaging

| | |
|---|---|
| When | A topic, a queue, a key, a retry, or what happens when a message is given up |
| Type | *Use the skill storefront-configure-messaging. When the request for stock is given up after its retries, the order must not wait for ever. Say where an operator finds the message.* |
| The agent | declares the route and the retry rule, and answers the process that waited |
| In this repository | `commerce/src/Storefront.Commerce.Api/Program.cs`; `commerce/src/Storefront.Commerce.Api/Hosting/GivenUpMessages.cs`; scenario S16 |

### Integrate two contexts

| | |
|---|---|
| When | Something is owned elsewhere: another module, another backend, another company |
| Type | *Use the skill storefront-integrate-contexts. The warehouse must learn of an order that is ready to ship, and the shop of a parcel that has left. The two share no code. Hold the contract with a test.* |
| The agent | names who depends on whom, chooses a message or a call and says why, gives the reader its own copy of the contract, and adds the test that holds the two together |
| In this repository | `Ordering/Domain/Events/OrderReadyToShip.cs`; `fulfillment/src/Storefront.Fulfillment.Application/Contracts`; `tests/Storefront.Contracts.Tests`; scenario S18 |

### Verify the behaviour

| | |
|---|---|
| When | Before anybody calls it done |
| Type | *Use the skill storefront-verify-business-behavior. One basket must never become two orders. Prove it against the running backend, not by reading the code, and tell me what you counted.* |
| The agent | tests a rule by its code, and a guarantee by running it: eight requests at the same moment, then a count of what exists |
| In this repository | `scripts/scenarios.sh`, scenario S15; [lessons.md](lessons.md), for what it found the first time |

## 6. Working well with an agent here

| Do | Because |
|---|---|
| Name the skill in what you type | An agent that is told which procedure to follow reads it; an agent that is left to choose sometimes does not |
| Write the criteria, and the boundary | "Change nothing outside the analytics folder" was kept by both agents |
| Say what was approved, and what was not | The skills tell the agent never to invent a business rule. It can only keep to that if it knows which rules exist |
| Ask for the real output of the tests | Both agents reported what they had not proved. Ask, and read that part first |
| Run the scenarios yourself | A handler test replaces the database with a fake. The SQL of section 3 was first run by a person |
| Let Codex run the tests | In its default sandbox it cannot: the test runner needs local sockets. Run them yourself, or start Codex with a sandbox that allows them |
| Change `docs/business.md` with the code | A rule that is not listed there does not exist for the next agent |

## 7. Changing the skills

| The skills | Are written in | And written out by |
|---|---|---|
| `mpcore-...`, in each backend | MP Core's template. They arrive with the backend, for the choices it was generated with | `mpcore new backend` |
| `storefront-...`, at the root | [`scripts/agents/sync-skills.py`](../scripts/agents/sync-skills.py) | `python3 scripts/agents/sync-skills.py` |

A backend of your own has the first kind only. When your team learns something an agent has to know,
write a skill of your own next to them, without the prefix `mpcore-`: the generator leaves it alone.
