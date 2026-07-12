# Settl

Household debt-tracking app: shared ledger (expenses, IOUs, recurring costs), flexible
splits, pure-ledger settlement. Product brief: `docs/specs/product-brief.md`.

## Layout

- `apps/web` — React 19 + Vite SPA. TanStack Router (file-based, `src/routes/`),
  TanStack Query, Tailwind v4, shadcn **Luma style on Base UI** (`base-luma`).
- `apps/api` — ASP.NET Core Minimal API, .NET 10, SQLite via EF Core (no
  SQLite-specific features — Postgres later, ADR-0004).
- `packages/api-client` — TS types generated from OpenAPI. Regenerate in the same PR
  as any API-shape change.
- `docs/` — ADRs, specs, tech-debt. Read `docs/README.md` for the workflow.

## Commands (from root)

- `pnpm dev:api` / `pnpm dev:web` — API on :5000, web on :5173
- `pnpm verify` — full check: dotnet build+test, web lint+build (build includes `tsc -b`)
- `dotnet test` — API tests only
- Add shadcn components from `apps/web`: `pnpm dlx shadcn@latest add <name>`

## Architecture rules

- **Business logic lives in the API** (ADR-0006): splitting math, recurrence,
  balances, reminders. The web app renders and calls; it does not compute.
- Money is integer minor units, never floats.
- User↔household is many-to-many (multi-household is an open question — don't
  hard-code single-household assumptions).

## Decision workflow

- Important/irreversible decision → suggest `/grill` (opinions) or `/grill-with-docs`
  (external tech; research first). ADRs come ONLY from grill sessions, and only for
  decisions that are expensive to reverse. When mid-task work smells ADR-worthy,
  propose a grill — don't decide silently, don't write the ADR unprompted.
- Deliberate shortcut → `docs/tech-debt/` entry immediately (see its README for format).
- shadcn note: `components/ui/` files are owned-but-generated; switching styles
  rewrites them, so keep customizations in wrapper components where practical.
