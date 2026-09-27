---
name: storefront-apply-business-audit
description: "Record who did what in one of Storefront's backends, and decide what is never recorded. Finds the backend the task belongs to (commerce, fulfillment or analytics) and hands over to that backend's MP Core skill."
---

# Record who did what in one of Storefront's backends

This repository holds three backends. Each carries MP Core's skill `mpcore-apply-business-audit`, written for that
backend's own shape, transport and broker. This skill finds the right one. It has no procedure of its
own.

## Steps

1. **Decide which backend the task belongs to.** `AGENTS.md` at the root of the repository has the
   table. A task that needs two backends is two changes and a contract between them: say so, and ask
   which one to begin with.
2. **Read that backend's manifest**, `<backend>/.mpcore/template-manifest.json`. Its shape, transport
   and broker were decided when the backend was generated.
3. **Read the body of the skill for that backend, and follow it:**

   - Commerce (a modular monolith: Catalog, Basket, Ordering, Payments): `commerce/.mpcore/skills/mpcore-apply-business-audit/SKILL.md`
   - Fulfillment (a service: the warehouse): `fulfillment/.mpcore/skills/mpcore-apply-business-audit/SKILL.md`
   - Analytics (a service: the sales figures): `analytics/.mpcore/skills/mpcore-apply-business-audit/SKILL.md`

4. **Hold to the rules of this repository** (`AGENTS.md`): the three backends share no code; a module
   writes only its own data; a defect of MP Core is reported, never worked around; a guarantee is proved
   by running it.
5. **Change `docs/business.md` with the code.** A rule that is not listed there does not exist.

## What the result looks like, in this repository

A price change, and a price change that was refused: `Catalog/Application/Commands/ChangeProductPrice.cs` and `commerce/src/Storefront.Commerce.Infrastructure/Audit/AuditPolicyConfiguration.cs`. The warehouse audits a dispatch and not the address. Scenario S3.

Paths that begin with a module's name are in Commerce, inside that module's one project:
`commerce/src/Modules/<Module>/Storefront.Commerce.Modules.<Module>/`.
