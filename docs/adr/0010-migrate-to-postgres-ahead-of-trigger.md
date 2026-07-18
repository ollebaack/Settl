# ADR-0010: Postgres, dev and prod (migrated ahead of the original SQLite-first trigger)

- **Status:** accepted
- **Date:** 2026-07-13 (supersedes the SQLite-first decision, was ADR-0004, 2026-07-12)

## Context

The original database decision (**ADR-0004**, 2026-07-12) was **SQLite first, Postgres
when hosting demands it**: SQLite via EF Core for dev and first deployment (single
instance, file next to the app), migrating to Postgres only when concurrent instances or
managed backups were needed. Its load-bearing rule — **no SQLite-specific features**, stay
on portable EF Core constructs so the swap is provider + connection string — carries
forward. Its stated migration trigger ("first real multi-user deployment") had **not**
fired: there was still no real production data.

We moved anyway. Waiting means the eventual migration (schema + data migration, a full
test pass against Postgres) happens later, under time pressure, with real household data at
stake instead of none. Moving while there's nothing to lose is the cheaper time. Separately,
ADR-0008 established that local dev needs no Docker because Settl had no `ContainerResource`s
— Aspire's idiomatic Postgres (`builder.AddPostgres(...)`) is `AddContainer`-backed, which
changes that.

## Decision

We migrate from SQLite to Postgres now, ahead of ADR-0004's trigger. Local dev runs
Postgres via Aspire's `AddPostgres` integration (container-backed). Production runs
Postgres as a self-hosted container in the single-VPS Docker Compose stack (ADR-0009);
the deployment ADR-0014 later moved it to a Dokploy-managed Postgres service for built-in
backups.

## Consequences

Docker Desktop/Podman becomes a required local-dev dependency — ADR-0008's "no Docker
needed" consequence no longer holds. In exchange, dev and prod run the same engine,
catching SQLite-vs-Postgres type mismatches locally instead of first in production, and the
harder migration happens now, while there's no real data to move. Self-hosting Postgres kept
infra cost at zero, at the price of owning backups/upgrades — a gap since closed by ADR-0014
(Dokploy-managed Postgres) and ADR-0015 (off-box backups to R2). Revisit if the VPS can't
handle API + Postgres load together.

## Sources

- ADR-0004: SQLite first, Postgres when hosting demands it (superseded by this ADR; its
  "no SQLite-specific features" portability rule is retained)
- ADR-0008: Aspire local dev orchestration (Docker-not-needed consequence revised)
- ADR-0009 / ADR-0014: deployment topology (gained a postgres service, then a managed one)
