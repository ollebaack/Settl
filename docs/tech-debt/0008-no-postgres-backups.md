# 0008: Postgres backups — RESOLVED 2026-07-16

**Status:** Resolved. Scheduled Postgres backups run to Cloudflare R2, are verified in the
bucket, restore-proven into a throwaway database, and hardened against deletion with a
7-day R2 bucket lock. Kept for history; live config lives in `docs/ops/production.md` →
Backups, decisions in ADR-0014 (mechanism) and ADR-0015 (provider + hardening).

**What was done (all 2026-07-16):**
1. ✅ Cloudflare R2 bucket `settl-pg-backups` (EU jurisdiction) + scoped Account API token
   (`settl-dokploy-backups`, Object Read & Write, this bucket only).
2. ✅ Dokploy S3 destination `cloudflare-r2` (Cloudflare R2 Storage provider, region
   `auto`, `.eu.` endpoint); Test passed.
3. ✅ Backup job: database `Settl`, cron `0 3 * * *` (UTC), prefix `settl-prod/`, keep 14.
4. ✅ Manual run succeeded; dump verified at
   `settl-pg-backups/settl-settlpostgres-w2kk0z/settl-prod/<ISO>.sql.gz` (~7 KB gzip).
5. ✅ Restore drill: spun up a throwaway `settl-restore-test` Postgres service, restored
   the dump into it (`pg_restore … --clean --if-exists` reported success), deleted the
   throwaway. Production `settl-postgres` was never touched.
6. ✅ Anti-deletion hardening (ADR-0015): R2 Bucket Lock Rule `settl-backups-7day-lock`,
   7-day retention. Chosen over object versioning because versioning isn't
   dashboard-configurable in R2 (S3-API/Wrangler only), and bucket lock is a stronger
   guarantee. 7 days sits safely under the job's 14-backup retention, so Dokploy's own
   cleanup deletes (of ~14-day-old objects) never hit still-locked objects.
