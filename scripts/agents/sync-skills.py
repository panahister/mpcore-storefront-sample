#!/usr/bin/env python3
"""Writes the skills an AI coding agent finds at the root of this repository.

Each backend carries MP Core's ten skills, written for that backend's own shape, transport and broker.
An agent that is started in a backend's folder finds them. An agent that is started at the root of
this repository finds none: Codex looks in .agents/skills from its working directory up to the root of
the repository, and Claude Code looks into a folder below only once it works there.

So the root has ten skills of its own. Each does one thing: it finds the backend a task belongs to, and
hands over to that backend's skill. It carries no procedure of its own, so there is one body to keep.

    python3 scripts/agents/sync-skills.py

writes .agents/skills (Codex) and .claude/skills (Claude Code). Edit this file, not what it writes.
"""
import os
import shutil

BACKENDS = [("commerce", "Commerce", "a modular monolith: Catalog, Basket, Ordering, Payments"),
            ("fulfillment", "Fulfillment", "a service: the warehouse"),
            ("analytics", "Analytics", "a service: the sales figures")]

# name of MP Core's skill, what it is used for here, and where this repository shows the result
SKILLS = [
    ("plan-bounded-context",
     "Plan a bounded context or a capability of Storefront before any code, with its aggregates, rules, commands and queries, and what is still unclear",
     "The plan of the warehouse: `docs/business.md`, section 9. One aggregate, five rules with a code each, three gRPC methods, and what the warehouse is never told (a price)."),
    ("implement-ddd-module",
     "Create or reshape a module of the Commerce backend, or the skeleton of a service",
     "The Payments module: `commerce/src/Modules/Payments`. One project with Domain, Application and Infrastructure folders, a Contracts project next to it, registered in `commerce/src/Storefront.Commerce.Api/Hosting/HandlerAssemblies.cs`."),
    ("implement-vertical-slice",
     "Implement one business capability of Storefront end to end, from the rule and the handler to the endpoint, its authorization and its tests",
     "Changing a price: the rule `Catalog/Domain/Rules/PriceMoveMustBeGradual.cs`, the handler `Catalog/Application/Commands/ChangeProductPrice.cs`, the endpoint in `commerce/src/Storefront.Commerce.Api/Rest/Endpoints/StorefrontEndpoints.cs`, the tests in `commerce/tests`, and scenario S3."),
    ("design-transport-contract",
     "Design or change a REST or gRPC contract of one of Storefront's backends",
     "The warehouse's gRPC contract: `fulfillment/src/Storefront.Fulfillment.Api/Protos/storefront_fulfillment.proto`. Commerce's REST endpoints and their failures: `docs/business.md`, section 7."),
    ("apply-security",
     "Restrict a capability of Storefront to a role, or to the owner of a record, from the validated token only",
     "Who may see an order: `Ordering/Application/OrderAccess.cs`, where another shopper's order is 'not found' and never 'forbidden'. The role policies: `commerce/src/Storefront.Commerce.Api/Hosting/StorefrontPolicies.cs`. Scenarios S11 and S21."),
    ("apply-business-audit",
     "Record who did what in one of Storefront's backends, and decide what is never recorded",
     "A price change, and a price change that was refused: `Catalog/Application/Commands/ChangeProductPrice.cs` and `commerce/src/Storefront.Commerce.Infrastructure/Audit/AuditPolicyConfiguration.cs`. The warehouse audits a dispatch and not the address. Scenario S3."),
    ("apply-observability",
     "Add logs, metrics, traces or a health check to one of Storefront's backends, without leaking a secret",
     "The order metrics: `Ordering/Application/OrderingMetrics.cs`. A message that never prints its secret: the `ToString` of `Basket/Application/Commands/Checkout.cs`, and the test `A_message_never_prints_its_secret`. Health: `Hosting/HostHealthChecks.cs`."),
    ("configure-messaging",
     "Declare a topic, a queue, a partition key, a retry rule, or what happens when a message is given up",
     "Commerce's routes and retry rules: `commerce/src/Storefront.Commerce.Api/Program.cs`. Giving a message up is an answer: `commerce/src/Storefront.Commerce.Api/Hosting/GivenUpMessages.cs`. Scenario S16."),
    ("integrate-contexts",
     "Make two modules, or two of Storefront's backends, work together without sharing code or a transaction",
     "The shop and the warehouse: what Commerce sends (`Ordering/Domain/Events/OrderReadyToShip.cs`), the warehouse's own copy (`fulfillment/src/Storefront.Fulfillment.Application/Contracts`), and the test that holds them together (`tests/Storefront.Contracts.Tests`). Scenario S18."),
    ("verify-business-behavior",
     "Prove a capability of Storefront: a rule by its code, a guarantee by running it",
     "`scripts/scenarios.sh`: every step states what it expects. Scenario S15 checks one basket out eight times at the same moment; `docs/lessons.md` tells what it found the first time."),
]

BODY = """---
name: storefront-{short}
description: "{description}. Finds the backend the task belongs to (commerce, fulfillment or analytics) and hands over to that backend's MP Core skill."
---

# {title}

This repository holds three backends. Each carries MP Core's skill `mpcore-{short}`, written for that
backend's own shape, transport and broker. This skill finds the right one. It has no procedure of its
own.

## Steps

1. **Decide which backend the task belongs to.** `AGENTS.md` at the root of the repository has the
   table. A task that needs two backends is two changes and a contract between them: say so, and ask
   which one to begin with.
2. **Read that backend's manifest**, `<backend>/.mpcore/template-manifest.json`. Its shape, transport
   and broker were decided when the backend was generated.
3. **Read the body of the skill for that backend, and follow it:**

{bodies}
4. **Hold to the rules of this repository** (`AGENTS.md`): the three backends share no code; a module
   writes only its own data; a defect of MP Core is reported, never worked around; a guarantee is proved
   by running it.
5. **Change `docs/business.md` with the code.** A rule that is not listed there does not exist.

## What the result looks like, in this repository

{example}

Paths that begin with a module's name are in Commerce, inside that module's one project:
`commerce/src/Modules/<Module>/Storefront.Commerce.Modules.<Module>/`.
"""

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    for target in (".agents/skills", ".claude/skills"):
        folder = os.path.join(ROOT, target)
        if os.path.isdir(folder):
            for name in os.listdir(folder):
                if name.startswith("storefront-"):
                    shutil.rmtree(os.path.join(folder, name))
        for short, description, example in SKILLS:
            bodies = "".join(
                f"   - {name} ({what}): `{folder_}/.mpcore/skills/mpcore-{short}/SKILL.md`\n"
                for folder_, name, what in BACKENDS)
            text = BODY.format(short=short, description=description, example=example, bodies=bodies,
                               title=description.split(",")[0])
            path = os.path.join(folder, f"storefront-{short}", "SKILL.md")
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8") as f:
                f.write(text)
    print(f"wrote {len(SKILLS)} skills into .agents/skills and .claude/skills")


if __name__ == "__main__":
    main()
