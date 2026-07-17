# ADR-0008: .NET Aspire orchestrates local dev startup

- **Status:** accepted (fixed-port consequence revised by [ADR-0025](0025-per-worktree-dynamic-dev-ports.md))
- **Date:** 2026-07-12

## Context

Local dev currently needs two terminals (`pnpm dev:api`, `pnpm dev:web`). .NET Aspire
(13.x, NuGet-only since Aspire 9 — no SDK workload) can start both the API and the Vite
frontend from one `dotnet run` via an AppHost project, using `AddViteApp`/`AddNpmApp`
(`Aspire.Hosting.NodeJs`), with a dashboard for unified logs/traces. Aspire's JS support
is dev/build-only — it never serves the frontend in production — so this is purely a
local-dev-experience change, not a deployment change.

Aspire's container runtime requirement (Docker Desktop/Podman) only applies to
`ContainerResource`s added via `AddContainer` (databases, caches, etc.). Plain project
resources — the API and the Vite dev server — run as native host processes under
Aspire's DCP, not in containers. Settl has no container resources, so this setup adds
**no** Docker dependency.

Current known rough edges (per aspire.dev docs and open microsoft/aspire issues, mid-2026):
`AddViteApp`'s HMR breaks under Aspire's default proxy and needs an undocumented
`IsProxied = false` workaround; and the exposed Vite port is reassigned randomly on
every run unless pinned manually.

## Decision

We will add an AppHost project that starts the API and web dev server together via
`AddViteApp`, applying the `IsProxied = false` / fixed-port workarounds needed to keep
HMR and a stable `:5173` working.

## Consequences

The Vite integration is immature — expect to hand-hold `IsProxied`/port config past
what's documented, and to hit further rough edges as Aspire's JS support evolves. In
exchange, `dotnet run` on the AppHost replaces two terminals with one, and the Aspire
dashboard gives unified logs across API and web during local dev, with no new runtime
dependency (no Docker needed — see Context). `pnpm dev:api`/`pnpm dev:web` remain
available as a fallback if Aspire breaks. Production deployment is unaffected by this
ADR — Aspire's JS orchestration is build-only there; containerizing Settl for
deployment (Dockerfiles, `aspire deploy`, target environment) is a separate decision,
not covered here.

## Sources

- [Set up JavaScript apps in the AppHost](https://aspire.dev/integrations/frameworks/javascript/)
- [Aspire prerequisites and required tooling](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
- [AddViteApp HMR breaks under default proxy (issue #14470)](https://github.com/microsoft/aspire/issues/14470)
- [Unstable exposed port for AddViteApp (issue #12942)](https://github.com/microsoft/aspire/issues/12942)
- [Aspire dashboard standalone — dev-only, in-memory telemetry](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone)
- [Aspire container networking — Docker only required for ContainerResources](https://aspire.dev/fundamentals/container-networking/)
