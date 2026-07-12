# Settl

Household debt-tracking app. A shared ledger for roommates, couples, and families —
who owes whom, without the fintech energy. Pure ledger, no in-app payments.

## Stack

| Part | Tech |
| --- | --- |
| `apps/web` | React 19 + Vite SPA, TanStack Router/Query, Tailwind v4, shadcn (Luma style, Base UI) |
| `apps/api` | ASP.NET Core Minimal API (.NET 10), SQLite for now |
| `packages/api-client` | TypeScript types generated from the API's OpenAPI spec |
| `docs/` | ADRs, specs, tech debt — see [docs/README.md](docs/README.md) |

## Getting started

```sh
pnpm install
pnpm dev:api   # API on http://localhost:5000
pnpm dev:web   # Web on http://localhost:5173
```

## Verify

```sh
pnpm verify    # builds + tests API, lints + typechecks + builds web
```
