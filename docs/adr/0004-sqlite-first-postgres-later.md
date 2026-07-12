# ADR-0004: SQLite first, Postgres when hosting demands it

- **Status:** accepted
- **Date:** 2026-07-12

## Context

No hosting decision has been made. A household ledger has tiny write volume. Standing up
cloud Postgres now adds cost and ceremony before there is a single user.

## Decision

We will use SQLite via EF Core for development and first deployment (single instance,
file next to the app). Migrate to Postgres when we need concurrent instances or managed
backups.

## Consequences

Zero-config local dev. **Rule: no SQLite-specific features** — stay on portable EF Core
constructs so the swap is provider + connection string. SQLite's loose typing can mask
issues Postgres would catch; the migration moment must include a data-migration script
and a test pass against Postgres. Revisit trigger: first real multi-user deployment.
