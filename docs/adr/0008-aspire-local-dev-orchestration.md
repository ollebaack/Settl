# ADR-0008: .NET Aspire orchestrates local dev startup, with per-worktree dynamic ports

- **Status:** accepted (dynamic-port revision added 2026-07-17, was ADR-0025)
- **Date:** 2026-07-12

## Context

Local dev previously needed two terminals (`pnpm dev:api`, `pnpm dev:web`). .NET Aspire
(NuGet-only since Aspire 9 — no SDK workload) can start both the API and the Vite
frontend from one command via an AppHost project, using `AddViteApp`
(`Aspire.Hosting.NodeJs`), with a dashboard for unified logs/traces. Aspire's JS support
is dev/build-only — it never serves the frontend in production — so this is purely a
local-dev-experience change, not a deployment change. `AddViteApp`'s HMR breaks under
Aspire's default proxy and needs the `IsProxied = false` workaround (microsoft/aspire
#14470).

The original decision pinned the web to `:5173` and the API to `:5000`. That broke the
moment a second `pnpm dev` ran from another git worktree — the fixed ports fail to bind,
so parallel isolated stacks (needed to browser-verify new API-backed UI while another
session holds the ports) were impossible. **ADR-0025** (2026-07-17) revised the port
story to fix this, and is folded in below.

## Decision

- **`pnpm dev` runs `aspire run --isolated`** — an AppHost project starts the API and web
  dev server together, and isolated mode (Aspire 13.2+) selects random free ports for the
  dashboard **and all service endpoints**, giving each worktree a collision-free range
  with its own user-secrets store and no port math in our code.
- The AppHost **injects the API's resolved URL into the web** via
  `.WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))` (the browser SPA only
  sees `VITE_`-prefixed vars; Aspire service discovery is server-side only) and keeps
  `IsProxied = false` for HMR (#14470).
- In **Development only**, CORS reflects any `localhost`/`127.0.0.1` origin with
  credentials (cookie auth uses `AllowCredentials()`, which forbids `AllowAnyOrigin()`);
  **Production stays strictly pinned** to the `Web:BaseUrl` origin.
- Agents find their ports from the dashboard URL that `aspire run` prints (and the
  dashboard's resource list). `pnpm dev:api`/`pnpm dev:web` remain the CLI-free fallback.

Aspire adds **no Docker dependency of its own**: container runtime is only required for
`ContainerResource`s (`AddContainer`). Plain project resources — the API and Vite dev
server — run as native host processes under Aspire's DCP. (Postgres later added a
container-backed resource per ADR-0010, so Docker *is* now required — see there.)

## Consequences

`dotnet run`/`aspire run` replaces two terminals with one, and the dashboard gives unified
logs across API and web. The `aspire` CLI moves into the daily inner loop (already required
for deployment publishing). Dev ports are no longer predictable — they change per run and
must be read from the dashboard/console, not bookmarked (we rejected deterministic
per-worktree offsets: hand-rolled port math across five ports fighting Aspire's native
feature). The Vite integration is immature — expect to hand-hold `IsProxied`/port config
past what's documented. **Risk to verify:** HMR forces the Vite endpoint proxyless
(#14470 still open); isolated-mode randomization is documented for proxied endpoints, so
confirm it reaches the proxyless Vite `PORT` — fallback is a per-instance Vite port offset
or proxied Vite once #14470 lands. Revisit if #14470 is fixed (drop the proxyless
workaround) or if Aspire adds a `dotnet run`-compatible isolated toggle (drop the
CLI-in-inner-loop cost). Production deployment is unaffected — Aspire's JS orchestration is
build-only there; containerizing Settl for deployment is a separate decision (ADR-0009/0014).

## Sources

- [Aspire isolated mode — parallel development (devblogs, 2026)](https://devblogs.microsoft.com/aspire/aspire-isolated-mode-parallel-development/)
- [Set up JavaScript apps in the AppHost — `VITE_` env injection, `PORT`](https://aspire.dev/integrations/frameworks/javascript/)
- [AddViteApp HMR breaks under default proxy (issue #14470)](https://github.com/microsoft/aspire/issues/14470)
- [Aspire container networking — Docker only required for ContainerResources](https://aspire.dev/fundamentals/container-networking/)
- [Aspire dashboard standalone — dev-only, in-memory telemetry](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone)
