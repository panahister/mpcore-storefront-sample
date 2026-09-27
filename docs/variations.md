# Variations

Storefront has one branch, `main`, and everything on it runs on every change. What can be chosen is a
**switch**, not a branch: a branch that is kept for a variant falls behind the day after it is made, and
nobody finds it.

| Variation | Switch | Default | Proved by |
|---|---|---|---|
| [The edge verifies tokens as well](#the-edge-verifies-tokens-as-well) | `EDGE_AUTH=keycloak` | `off` | scenario S21 |
| [Where MP Core comes from](#where-mp-core-comes-from) | `-p:MPCoreSource=NuGet` or `Local` | `Local` when a clone of MP Core sits next to this repository, `NuGet` otherwise | CI, both |
| [Without the observability stack](#without-the-observability-stack) | `scripts/up.sh --no-observability` | with | CI |
| [On Linux](#on-linux) | `STOREFRONT_BIND=0.0.0.0` | not set | CI |

A switch is set for one command (`EDGE_AUTH=keycloak scripts/up.sh`) or for good, in `infrastructure/.env`.

## The edge verifies tokens as well

```bash
EDGE_AUTH=keycloak scripts/up.sh
```

```bash
scripts/scenarios.sh S20 S21
```

To go back, `EDGE_AUTH=off scripts/up.sh`. The edge reads its routes again within a second; nothing is
restarted.

**What changes.** With `off`, Apache APISIX passes every request on, and a backend answers 401 to one
that has no token. With `keycloak`, APISIX verifies a token's signature with the public keys of the
Keycloak realm, and answers 401 itself. A request with no token, or with a token that Keycloak did not
sign, never reaches a backend.

**What does not change.** The backends verify every token, as before. This is not caution for its own
sake; the edge cannot know what a backend knows:

| Question | The edge | The backend |
|---|---|---|
| Was this token signed by Keycloak? | yes | yes, again |
| Was it issued **for this backend**? (the audience) | cannot know: it serves three | yes |
| May its holder do **this**? (the role) | cannot know | yes |
| Whose basket, whose order? | cannot know | yes, in the application layer |

Scenario S21 shows each line: a token that is no token, and a token rewritten to give its holder more
roles, are refused by the edge; a real token of a shopper is let through by the edge and refused by the
back office; the warehouse's token, signed by Keycloak and issued for other backends, is let through by the
edge and refused by Analytics. The last step shows the rewritten token to a backend directly, past the
edge: the backend refuses it alone.

**How it is built.** The routes that need a token share one plugin configuration, `needs-a-token`, in
[`infrastructure/apisix/apisix.template.yaml`](../infrastructure/apisix/apisix.template.yaml). The switch
fills it from `edge-auth.off.yaml` or `edge-auth.keycloak.yaml`, next to the template. With `keycloak` it
is APISIX's `openid-connect` plugin, which accepts a bearer token only and verifies it with the realm's
keys, without a call to Keycloak for each request. It adds no header that says who the caller is: a
backend would remove it.

**One name for the issuer.** A token names who issued it. The backends reach Keycloak at `localhost`, the
edge reaches it inside the compose network as `keycloak`. Keycloak is told its public name
(`KC_HOSTNAME`), so that a token carries the same issuer whoever asked for it.

**Not proved here.** That the edge refuses a token whose lifetime is over: a token lives thirty minutes,
and a scenario does not wait that long.

**The principle.** Defence in depth; and *never trust, always verify* (NIST SP 800-207). MP Core's
decision is ADR-007: a gateway that validates is one more wall, never a replacement.

## Where MP Core comes from

| Value | MP Core is |
|---|---|
| `NuGet` | the packages from nuget.org, version `0.9.0` |
| `Local` | the source of a clone at `../mpcore`: a breakpoint in the framework works, and a change there is picked up by the next build |

```bash
dotnet build commerce/Storefront.Commerce.Backend.sln -p:MPCoreSource=NuGet
```

[`Directory.Build.targets`](../Directory.Build.targets) decides and says so in the build output.
`scripts/verify-against-packages.sh` packs a clone of MP Core and builds and tests the three backends
against the packed files, which `Local` never does.

## Without the observability stack

```bash
scripts/up.sh --no-observability
```

Leaves out the OpenTelemetry Collector, Jaeger, Prometheus and Grafana. The backends run the same; their
telemetry has nowhere to go, which never fails a request.

## On Linux

```bash
STOREFRONT_BIND=0.0.0.0 scripts/run.sh commerce
```

The backends listen on the loopback address. The edge runs in a container; Docker Desktop lets a
container reach that address, Docker on Linux does not. With `STOREFRONT_BIND` a backend listens where the
container can reach it. CI runs this way.

## Adding a variation

A variation earns its place when it answers a question a reader has, and when a scenario can prove it.
It is a switch with a default, a section here, and a scenario that is skipped, with the reason, when the
switch is off. It is built on a branch of its own and arrives on `main` through a pull request whose CI ran
both with the switch and without.
