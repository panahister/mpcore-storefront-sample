---
name: storefront-integrate-contexts
description: "Make two modules, or two of Storefront's backends, work together without sharing code or a transaction. Finds the backend the task belongs to (commerce, fulfillment or analytics) and hands over to that backend's MP Core skill."
---

# Make two modules

This repository holds three backends. Each carries MP Core's skill `mpcore-integrate-contexts`, written for that
backend's own shape, transport and broker. This skill finds the right one. It has no procedure of its
own.

## Steps

1. **Decide which backend the task belongs to.** `AGENTS.md` at the root of the repository has the
   table. A task that needs two backends is two changes and a contract between them: say so, and ask
   which one to begin with.
2. **Read that backend's manifest**, `<backend>/.mpcore/template-manifest.json`. Its shape, transport
   and broker were decided when the backend was generated.
3. **Read the body of the skill for that backend, and follow it:**

   - Commerce (a modular monolith: Catalog, Basket, Ordering, Payments): `commerce/.mpcore/skills/mpcore-integrate-contexts/SKILL.md`
   - Fulfillment (a service: the warehouse): `fulfillment/.mpcore/skills/mpcore-integrate-contexts/SKILL.md`
   - Analytics (a service: the sales figures): `analytics/.mpcore/skills/mpcore-integrate-contexts/SKILL.md`

4. **Hold to the rules of this repository** (`AGENTS.md`): the three backends share no code; a module
   writes only its own data; a defect of MP Core is reported, never worked around; a guarantee is proved
   by running it.
5. **Change `docs/business.md` with the code.** A rule that is not listed there does not exist.

## What the result looks like, in this repository

The shop and the warehouse: what Commerce sends (`Ordering/Domain/Events/OrderReadyToShip.cs`), the warehouse's own copy (`fulfillment/src/Storefront.Fulfillment.Application/Contracts`), and the test that holds them together (`tests/Storefront.Contracts.Tests`). Scenario S18.

Paths that begin with a module's name are in Commerce, inside that module's one project:
`commerce/src/Modules/<Module>/Storefront.Commerce.Modules.<Module>/`.
