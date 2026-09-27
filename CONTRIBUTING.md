# Contributing to the Storefront sample

The sample has one job: to show how a backend is built on MP Core, in code that runs. A change is welcome
when it makes that clearer or more true.

## What fits

- A defect: something that does not do what [docs/business.md](docs/business.md) says.
- A scenario that shows a behaviour nobody can see yet.
- A clearer name, a clearer document, a better source for a convention.

## What does not

- **A workaround for a defect of MP Core.** A defect the sample finds in MP Core is fixed in MP Core, and
  the sample then uses the fix. Report it at [MP Core](https://github.com/panahister/mpcore/issues).
- More business. Storefront is as large as it needs to be to show what it shows.
- A dependency the sample can do without.

## What a pull request needs

1. The tests pass, and the scenarios pass against the running backends. Report the real numbers.
2. **A guarantee is proved, not described.** A change that claims something about a failure, a race or a
   repeat comes with a test that was seen failing, or with a scenario step that fails without the change.
3. A Release build with no warnings: warnings are errors.
4. Documentation in English. A convention names its source: the pattern, who described it, and why it
   was chosen.
5. A business rule that is added or changed is added or changed in [docs/business.md](docs/business.md)
   too, with its code and the place it lives.

```bash
dotnet test commerce/Storefront.Commerce.Backend.sln
```

```bash
dotnet test fulfillment/Storefront.Fulfillment.Backend.sln
```

```bash
dotnet test analytics/Storefront.Analytics.Backend.sln
```

```bash
dotnet test tests/Storefront.Contracts.Tests
```

```bash
scripts/scenarios.sh
```

## Licence

By contributing you agree that your contribution is licensed under the [Apache License 2.0](LICENSE).
