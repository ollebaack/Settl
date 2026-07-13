# 0005: No off-box backup for the production SQLite file

**What:** The production SQLite database (ADR-0004, ADR-0009) lives on a single Docker
volume on one VPS, with no replication or off-box backup. A volume/disk failure loses
all household ledger data with no recovery path.

**Why we took it:** Pre-launch, no real user data exists yet, and setting up continuous
replication (e.g. Litestream to S3-compatible storage) before there's anyone to lose
data for is ceremony ahead of need — the same reasoning ADR-0004 used to defer Postgres.

**Trigger to pay it down:** Before onboarding any real household's data, or immediately
if a second person starts relying on the app. Add continuous backup (Litestream or
equivalent) replicating to off-box object storage at that point.
