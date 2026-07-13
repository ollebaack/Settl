# ADR-0009: Single-container Docker Compose deployment on a self-hosted VPS

- **Status:** accepted
- **Date:** 2026-07-12

## Context

No production deployment target has existed until now. ADR-0004 already committed to a
single SQLite instance ("file next to the app") and explicit cost-consciousness before
there's a single user. Aspire's `aspire publish`/`aspire deploy` reached GA in 2026 with
three real target families: Azure (Container Apps, App Service, AKS — all built for
scale-out via `azd`), Kubernetes, and Docker Compose (stable, aimed at self-hosted/
on-prem). SQLite cannot span multiple instances without an add-on like LiteFS, so the
scale-out targets buy nothing here and add real monthly cost. A plain VM with no
containers at all was also considered and rejected only narrowly — Docker Compose still
wins for reproducible builds and because `aspire publish` generates the Compose file
directly from the AppHost model.

Aspire's JS hosting (`AddViteApp`) is dev/build-only — official docs confirm it is never
the production web server; another resource must serve the built assets. Since the
native (Expo) app the product brief anticipates talks to the API directly over HTTP
(ADR-0006) and never touches however the web SPA's static files are served, the
API-serves-SPA vs. separate-static-server choice has no bearing on native-app readiness
— it's purely a today's-traffic-level call, and swapping from one to two containers
later is an infra-config change (add an nginx/Caddy service, remove the API's static
middleware), not an application rewrite.

Community best practice for SQLite in containers (Fly.io, general 2026 guidance): the DB
file must live on a persistent volume, never baked into the image, and migrations must
run at container startup, not in a release/build phase (release-phase steps typically
lack volume access).

## Decision

We will deploy Settl as a single Docker container: the API project's Dockerfile
multi-stage-builds the web SPA and copies its output into the API's static file root, so
one image serves both the API routes and the SPA. The SQLite file lives on a mounted
volume, with migrations running at container startup. We target Docker Compose on a
self-hosted VPS, generated via `aspire publish`, rather than Azure or Kubernetes.

## Consequences

Simplest possible production topology today: one image, one process, one VPS, no cloud
container-orchestration cost or complexity. Splitting the SPA into its own
nginx/Caddy container later — if traffic or caching needs justify it — is a config
change, not a rewrite. We accept no off-box SQLite backup for now (see
`docs/tech-debt/0005-sqlite-backup-strategy.md`); a volume/disk failure loses all data
until that's addressed. Revisit this ADR when: concurrent-write load forces the
Postgres migration (ADR-0004's own trigger, which also reopens the scale-out question),
or when real user data makes the backup gap unacceptable.

## Sources

- [Aspire deployment overview](https://aspire.dev/deployment/)
- [Deploy Aspire apps to Azure targets with aspire deploy](https://aspire.dev/deployment/azure/)
- [Aspire on Kubernetes overview](https://aspire.dev/deployment/kubernetes/)
- [Aspire roadmap 2026→2027 — Docker Compose GA, deploy/publish GA](https://github.com/microsoft/aspire/discussions/18581)
- [Fly.io SQLite persistence guidance — volumes, migrations at startup](https://fly.io/docs/rails/advanced-guides/sqlite3/)
- [Set up JavaScript apps in the AppHost — dev/build-only, not a production server](https://aspire.dev/integrations/frameworks/javascript/)
