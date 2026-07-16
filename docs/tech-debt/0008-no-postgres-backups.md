# 0008: Production Postgres has no backups configured

**What:** The `settl-postgres` Dokploy database service has no scheduled backups. A
volume/disk failure on the VPS loses all household ledger data with no recovery path.
ADR-0014 decided *how* this will eventually be solved — Dokploy's native scheduled
`pg_dump`-to-S3 feature — but deciding the mechanism isn't the same as it being on;
nothing is actually backed up today.

**Why we took it:** Configuring the backup feature needs S3-compatible storage
credentials (a bucket + access keys), which weren't available during the Dokploy setup
session. Getting the app live took priority over this follow-up step.

**Trigger to pay it down:** Before onboarding any real household's data, or immediately
if a second person starts relying on the app — same trigger the SQLite-era version of
this debt used. Get S3-compatible storage credentials (Backblaze B2, AWS S3, MinIO,
etc.), then configure the schedule under `settl-postgres` → Backups in Dokploy.
