# 0005: No off-box backup for the production database

**What:** The production database (Postgres per ADR-0010, self-hosted per ADR-0009) lives
on a single Docker volume on one VPS, with no replication or off-box backup. A
volume/disk failure loses all household ledger data with no recovery path. This debt
originated when the database was SQLite (ADR-0004) and carries forward unchanged after
the Postgres migration (ADR-0010) — the engine changed, the backup gap didn't.

**Why we took it:** Pre-launch, no real user data exists yet, and setting up continuous
off-box backups (e.g. `pg_dump`/WAL archiving to S3-compatible storage) before there's
anyone to lose data for is ceremony ahead of need — the same reasoning ADR-0004 used to
defer Postgres in the first place.

**Trigger to pay it down:** Before onboarding any real household's data, or immediately
if a second person starts relying on the app. Add continuous off-box backups (WAL
archiving/`pg_dump` on a schedule, or a managed backup add-on) at that point.
