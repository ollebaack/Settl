# Settl

Household debt-tracking app. A shared ledger for roommates, couples, and families —
who owes whom, without the fintech energy. Pure ledger, no in-app payments.

## Stack

| Part | Tech |
| --- | --- |
| `apps/web` | React 19 + Vite SPA, TanStack Router/Query, Tailwind v4, shadcn (Luma style, Base UI) |
| `apps/api` | ASP.NET Core Minimal API (.NET 10), Postgres via EF Core |
| `packages/api-client` | TypeScript types generated from the API's OpenAPI spec |
| `docs/` | ADRs, specs, tech debt — see [docs/README.md](docs/README.md) |

## Getting started

Install Node.js 22.12+ (or a newer supported release), pnpm 10.18.3, and the
.NET 10 SDK, including the ASP.NET Core runtime and targeting pack. Docker or
Podman is required to run the local Postgres container.

On Omarchy, install the .NET prerequisites with:

```sh
omarchy pkg add dotnet-sdk-10.0 aspnet-runtime-10.0 aspnet-targeting-pack-10.0
```

If package downloads return 404, run `omarchy update` before retrying.

```sh
pnpm run setup # installs workspace dependencies and restores .NET tools/packages
pnpm dev      # starts API, web, and Postgres through Aspire
```

The setup script can also be run with `bash scripts/setup.sh`. It is safe to
rerun and does not require secrets or a running database. Aspire's dashboard
shows the development URLs.

For Orca worktrees, set the project's setup command to `bash scripts/setup.sh`.

## Verify

```sh
pnpm verify    # builds + tests API, lints + typechecks + builds web
```
