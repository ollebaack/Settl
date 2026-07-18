# ADR-0014: Production deployment — single-image Docker Compose on a self-hosted VPS via Dokploy

- **Status:** accepted
- **Date:** 2026-07-14 (consolidates the deployment topology, was ADR-0009, and the
  Cloudflare-edge decision, was ADR-0013)

## Context

This is the canonical production-deployment ADR. It absorbs two earlier decisions:

- **ADR-0009 (2026-07-12) — single-container Docker Compose on a self-hosted VPS.** Aspire's
  `aspire publish` generates a Docker Compose file directly from the AppHost model, targeting
  self-hosted/on-prem. Azure Container Apps / AKS / Kubernetes were rejected: they're built
  for scale-out this app doesn't need and add real monthly cost. A plain VM with no containers
  lost only narrowly — Compose wins for reproducible builds. The **standing decisions** from
  ADR-0009: the API project's Dockerfile multi-stage-builds the web SPA and copies its output
  into the API's static file root, so **one image serves both API routes and the SPA**;
  migrations run at **container startup** (not a release phase); Docker Compose over
  Azure/K8s. Splitting the SPA into its own nginx/Caddy container later is a config change,
  not a rewrite. (Only the SQLite pieces of ADR-0009 are dead — replaced by Postgres in
  ADR-0010.)
- **ADR-0013 (2026-07-14, draft) — Cloudflare as DNS + edge proxy.** Point nameservers at
  Cloudflare, proxy production traffic (orange-cloud) in front of the VPS for free
  TLS/CDN/WAF/DDoS, with the VPS firewall locked to Cloudflare's published IP ranges so the
  protection can't be bypassed by hitting the origin IP directly. Hosting the app itself on
  Cloudflare (Workers/Pages/D1) was rejected — a rewrite for speculative pre-launch edge
  caching. **Superseded by this ADR** on the TLS mechanism only (Origin CA cert → Let's
  Encrypt); the proxy-in-front and firewall-allowlist decisions stand.

While registering settlapp.se we discovered the target VPS already runs
[Dokploy](https://dokploy.com/), a self-hosted PaaS managing its own Traefik reverse proxy,
TLS certificates, and a native Postgres service with scheduled backups — none of which the
prior ADRs accounted for. A `/grill-with-docs` session (this ADR) researched Dokploy's
documented behavior before reconciling.

## Decision

We deploy Settl as a **Dokploy-managed Docker Compose project**, built from the same
Aspire-generated compose file and Dockerfile ADR-0009 already produces — Dokploy runs
`docker compose` under the hood and layers in its own domain/TLS routing, so ADR-0009's
build/image story is unchanged; only who runs `docker compose up` changes. **Postgres runs
as Dokploy's native Database service** rather than inside Settl's compose stack, so it gets
Dokploy's built-in scheduled `pg_dump` backups to S3-compatible storage (provider chosen in
ADR-0015). **Cloudflare proxies settlapp.se in Full (Strict)** using Dokploy's
auto-provisioned **Let's Encrypt** cert — Dokploy's officially supported configuration —
rather than a manually-managed Cloudflare Origin CA cert (hand-editing Traefik for Origin CA
is an unresolved pain point that breaks across Dokploy updates,
[dokploy#1839](https://github.com/Dokploy/dokploy/issues/1839)). The **VPS firewall still
restricts inbound 80/443 to Cloudflare's IP ranges** (from ADR-0013), which doesn't conflict
with Traefik's need for 80/443 (via Cloudflare) for routing and the Let's Encrypt HTTP-01
challenge.

## Consequences

Settl's connection string points at an externally-addressed (Dokploy-managed) Postgres
instance rather than a compose-internal one — a mechanical config difference from what
ADR-0010 assumed, not a rewrite. Cert-rotation ceremony is eliminated: Dokploy/Traefik
auto-renews Let's Encrypt certs, nothing to install or rotate on the box — a real
simplification over ADR-0013's Origin CA plan. This resolves the off-box-backup gap
(tech-debt/0005) as a decision. Dokploy becomes a new operational dependency — kept patched
(a security fix landed as recently as v0.29.3), its dashboard/auth secured in its own right.
The firewall rule still needs re-sync when Cloudflare updates its IP ranges. Revisit if the
Dokploy project is abandoned, or if traffic needs multi-VPS scale-out a single Dokploy
instance can't manage cleanly (the same trigger ADR-0009 named for the single-VPS approach).

Rollout is automated end-to-end: after CD pushes a new image to GHCR it triggers the Dokploy
deploy via its authenticated API (`POST /api/application.deploy` with an `x-api-key` token)
rather than the no-auth deploy webhook, which 404s across versions
([dokploy#2645](https://github.com/Dokploy/dokploy/issues/2645)). This adds a Dokploy API
token as a deploy-time secret (GitHub Actions, not the repo). The mechanics — required
secrets, the `Web__BaseUrl` fail-fast precondition, and rollback by pinning a
`sha-<full-sha>` image tag — live in `docs/ops/production.md`.

## Sources

- [Aspire deployment overview](https://aspire.dev/deployment/) / [Docker Compose GA, deploy/publish GA](https://github.com/microsoft/aspire/discussions/18581)
- [Set up JavaScript apps in the AppHost — dev/build-only, not a production web server](https://aspire.dev/integrations/frameworks/javascript/)
- [Dokploy: Cloudflare domain guide](https://docs.dokploy.com/docs/core/domains/cloudflare) — Full (Strict) + Let's Encrypt is the documented, supported combination
- [Dokploy: Databases](https://docs.dokploy.com/docs/core/databases) / [Backups](https://docs.dokploy.com/docs/core/backups) — native Postgres service with scheduled S3-compatible backups
- [Dokploy: Docker Compose domains](https://docs.dokploy.com/docs/core/docker-compose/domains) — native domain/TLS routing without hand-written Traefik labels
- [dokploy/dokploy#1839](https://github.com/Dokploy/dokploy/issues/1839) — Origin CA certificate support unresolved
- [Dokploy: API deployment](https://docs.dokploy.com/docs/api) / [dokploy/dokploy#2645](https://github.com/Dokploy/dokploy/issues/2645) — authenticated deploy API vs. the 404ing webhook
