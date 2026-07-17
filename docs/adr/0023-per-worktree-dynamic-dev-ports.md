# ADR-0023: Per-worktree dynamic dev ports via Aspire isolated mode

- **Status:** accepted
- **Date:** 2026-07-17
- **Revises:** [ADR-0008](0008-aspire-local-dev-orchestration.md) (the fixed-`:5173`/`:5000` consequence, not the "Aspire orchestrates dev" decision)

## Context

ADR-0008 pins the Vite web to `:5173` and the API to `:5000`, and the AppHost's
dashboard/OTLP/resource-service ports are fixed too. A second `pnpm dev` from another
git worktree therefore fails to bind, so agents can't run isolated stacks in parallel —
which blocked browser-verifying new API-backed UI while a session held `:5000`/`:5173`.
Aspire 13.2+ ships `--isolated` mode built for exactly this (git worktrees, parallel
checkouts, AI agents): it selects random free ports for the dashboard **and all service
endpoints** and gives each run its own user-secrets store, with no port math in our code.
Settl is on Aspire 13.4.6. Two things isolated mode does **not** cover and we must:
the browser SPA only sees `VITE_`-prefixed env vars (service discovery is server-side
only), and CORS is pinned to `http://localhost:5173` while cookie auth (ADR-0011) uses
`AllowCredentials()`, which forbids `AllowAnyOrigin()`.

## Decision

We will make `pnpm dev` run `aspire run --isolated` so every worktree gets a
collision-free random port range. The AppHost will inject the API's resolved URL into
the web via `.WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))` and stop
hardcoding the Vite port (keeping `IsProxied=false` for HMR, #14470). In **Development
only**, CORS will reflect any `localhost`/`127.0.0.1` origin with credentials; Production
stays strictly pinned to the `Web:BaseUrl` origin. Agents find their ports from the
dashboard URL that `aspire run` prints (and the dashboard's resource list).

## Consequences

The `aspire` CLI (already required for `publish:docker`, ADR-0009) moves into the daily
inner loop; `pnpm dev:api`/`pnpm dev:web` remain the CLI-free fallback. Dev ports are no
longer predictable — they change per run and must be read from the dashboard/console, not
bookmarked (we rejected deterministic per-worktree offsets: predictable but hand-rolled
across five ports, with hash-collision fallback logic, fighting Aspire's native feature).
Dev CORS is deliberately permissive to any local origin; this is Development-only and does
not touch the production allow-list. **Risk to verify on implementation:** HMR forces the
Vite endpoint proxyless (`IsProxied=false`, #14470 still open); isolated-mode randomization
is documented for proxied endpoints, so we must confirm it actually reaches the proxyless
Vite `PORT` — if not, the fallback is a per-instance Vite port (offset) or proxied Vite
once #14470 lands. Revisit if #14470 is fixed (drop the proxyless workaround) or if Aspire
adds a `dotnet run`-compatible isolated toggle (drop the CLI-in-inner-loop cost).

## Sources

- [Aspire isolated mode — parallel development (devblogs, 2026)](https://devblogs.microsoft.com/aspire/aspire-isolated-mode-parallel-development/)
- [Aspire inner-loop networking overview](https://aspire.dev/fundamentals/networking-overview/)
- [Set up JavaScript apps in the AppHost — `VITE_` env injection, `PORT`](https://aspire.dev/integrations/frameworks/javascript/)
- [AddViteApp HMR breaks under proxy / IsProxied workaround (issue #14470)](https://github.com/microsoft/aspire/issues/14470)
