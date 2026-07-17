# Settl

Household debt-tracking app: shared ledger (expenses, IOUs, recurring costs),
flexible splits, pure-ledger settlement. Brief: `docs/specs/product-brief.md`.

## Layout

- `apps/web` — React 19 + Vite SPA, TanStack Router/Query, Tailwind v4, shadcn
  (Luma, Base UI). Rules: `apps/web/CLAUDE.md`
- `apps/api` — .NET 10 Minimal API, EF Core + SQLite. Rules: `apps/api/CLAUDE.md`
- `packages/api-client` — TS types generated from OpenAPI
- `docs/` — ADRs, specs, tech-debt. Workflow: `docs/README.md`

## Commands (root)

- `pnpm dev` — Aspire AppHost via `aspire run --isolated`; starts API + web + Postgres
  together with **randomized per-run ports** so multiple worktrees run at once without
  collisions (ADR-0025). Requires the `aspire` CLI (`dotnet tool install -g Aspire.Cli`).
  **Find your ports:** `aspire run` prints the dashboard URL on startup; the dashboard's
  resource list shows the resolved web + API URLs (the web auto-learns the API port). The
  web origin is dynamic, so dev CORS accepts any localhost origin (prod stays pinned).
  Fallback (fixed :5000/:5173, no CLI): `pnpm dev:api` / `pnpm dev:web` in separate
  terminals (ADR-0008).
- `pnpm verify` — must pass before work is done
- `pnpm publish:docker` — generates a Docker Compose deployment (`docker-compose.yaml`
  and `.env`) for the single-container production image, via `aspire publish`.
  Requires the `aspire` CLI (`dotnet tool install -g Aspire.Cli`). Build the image
  separately with `docker build -f apps/api/Settl.Api/Dockerfile .`, set `API_IMAGE`
  in the generated `.env`, then `docker compose up` from
  `apps/api/Settl.AppHost/aspire-output` (ADR-0009).

## Rules

- Business logic lives in the API (ADR-0006); UIs render and call, never compute.
- Money = integer minor units. Timestamps = UTC.
- User↔household is many-to-many; never assume single household.
- API-shape change → regenerate `packages/api-client` in the same PR.
- No new dependencies without stating why first. No secrets in the repo.
- Important/irreversible decision → propose `/grill` (researches current docs first
  when external tech is involved). ADRs only from grill sessions. Deliberate shortcut →
  `docs/tech-debt/` entry.
