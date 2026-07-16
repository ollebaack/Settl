# ADR-0015: Postgres backups to Cloudflare R2 via Dokploy's native scheduled pg_dump

- **Status:** accepted
- **Date:** 2026-07-16

## Context

ADR-0014 decided the *mechanism* for production Postgres backups — Dokploy's native
scheduled `pg_dump` to S3-compatible storage — but left the storage provider unpicked, so
nothing is actually backed up ([tech-debt/0008](../tech-debt/0008-no-postgres-backups.md)).
A `/grill-with-docs` session (this ADR) compared Cloudflare R2, Backblaze B2, and AWS S3
against current pricing docs and Dokploy's actual backup implementation before choosing.
The research settled most of the question: Settl's database is a household ledger measured
in single-digit MB, so it sits permanently inside both R2's and B2's 10 GB always-free
recurring tiers; R2's headline "$0 egress" advantage is irrelevant to a backup workload
(you only egress on a rare, tiny restore); and Dokploy treats R2, B2, and any other
S3-compatible endpoint identically via `rclone` (Settings → Destinations: Name / Access
Key / Secret Key / Bucket / Region / Endpoint + Test), so no provider has a technical edge.
The decision therefore came down to a vendor trade-off, not cost or capability.

## Decision

We will back Dokploy's scheduled `pg_dump` backups of `settl-postgres` with a **Cloudflare
R2** bucket, configured as an S3-Compatible destination in Dokploy. We chose consolidation
onto Cloudflare — which already owns Settl's DNS and edge (ADR-0013) — over putting backups
in a separate vendor, on the reasoning that for a solo-maintained app the most *likely*
backup failure is an unnoticed misconfiguration (wrong key, silent `rclone` failure), and
every additional vendor/console/credential raises that risk more than it lowers the
correlated-account-failure risk. To offset the residual blast-radius concern (a Cloudflare
account compromise would reach edge, DNS, *and* backups), we will enable R2 object
versioning / bucket retention so backups cannot be trivially deleted, kept independent of
Dokploy's own retention count.

## Consequences

Recovery now depends on the same Cloudflare account that fronts production — a deliberate
single-trust-domain trade accepted for operational simplicity, partially mitigated by R2
object versioning. R2 requires a credit card on file even for the free tier (B2 would not
have); acceptable, and no charge is expected at Settl's scale. The provider choice is
cheaply reversible: re-pointing Dokploy to a different S3 destination is a settings change,
not a migration, so revisit if backups ever outgrow the 10 GB free tier, if Cloudflare
account risk becomes material (real households' financial data at meaningful scale), or if a
restore drill reveals R2/`rclone` incompatibilities. A backup that has never been restored
is not yet a backup: the first scheduled dump must be verified to land in the bucket, and a
trial `pg_restore` into a throwaway database run once, before this is considered done.

## Sources

- [Cloudflare R2 pricing](https://developers.cloudflare.com/r2/pricing/) — 10 GB always-free recurring tier, $0 egress, $0.015/GB beyond; credit card required to enable
- [Backblaze B2 pricing](https://www.backblaze.com/cloud-storage/pricing) — 10 GB free, no card required, $6/TB beyond, free egress to Cloudflare (the considered-but-not-chosen alternative)
- [Dokploy: Database backups](https://docs.dokploy.com/docs/core/databases/backups) — native `pg_dump` via `rclone`; Settings → Destinations fields; R2/B2/MinIO all "S3 Compatible"
- [Dokploy: Restore](https://docs.dokploy.com/docs/core/databases/restore) — restore path only guaranteed for Dokploy-generated dumps
