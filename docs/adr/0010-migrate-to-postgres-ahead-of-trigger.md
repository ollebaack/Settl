# ADR-0010: Migrate to Postgres ahead of ADR-0004's trigger

- **Status:** accepted
- **Date:** 2026-07-13

## Context

ADR-0004 deferred Postgres until "concurrent instances or managed backups" were needed,
to avoid cost and ceremony before there was a single user. That trigger hasn't fired —
there's still no real production data. But waiting means the eventual migration (schema
+ data migration script, a full test pass against Postgres per ADR-0004) happens later,
under time pressure, with real household data at stake instead of none. Moving now, while
there's nothing to lose, is the cheaper time to do it.

Separately, ADR-0008 established that local dev needs no Docker because Settl has no
`ContainerResource`s — Aspire's idiomatic way to add Postgres (`builder.AddPostgres(...)`)
is backed by `AddContainer`, which changes that.

## Decision

We will migrate from SQLite to Postgres now, rather than waiting for ADR-0004's trigger.
Local dev runs Postgres via Aspire's `AddPostgres` integration (container-backed).
Production runs Postgres as a self-hosted container added to the existing single-VPS
Docker Compose stack (ADR-0009), not a managed Postgres service.

## Consequences

Docker Desktop/Podman becomes a required local-dev dependency going forward — ADR-0008's
"no Docker needed" consequence no longer holds. In exchange, dev and prod now run the
same database engine, catching SQLite-vs-Postgres type mismatches locally instead of
first in production, and the harder migration happens now, while there's no real data to
move. Self-hosting Postgres on the same VPS keeps infra cost at zero over today's
one-container setup, but we own backups/upgrades ourselves — this carries forward the
same gap tech-debt/0005 already flags for SQLite (updated to cover Postgres) rather than
closing it via a managed service. Revisit if the VPS can't handle API + Postgres load
together, or if the backup gap becomes unacceptable once real household data exists.

## Sources

- ADR-0004: SQLite first, Postgres when hosting demands it (superseded by this ADR)
- ADR-0008: Aspire local dev orchestration (Docker-not-needed consequence revised)
- ADR-0009: Single-container Docker Compose deployment (topology gains a postgres service)
