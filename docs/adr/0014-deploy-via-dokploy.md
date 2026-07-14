# ADR-0014: Deploy via Dokploy; Postgres as a Dokploy database service; Cloudflare Full (Strict) with Dokploy-managed Let's Encrypt

- **Status:** accepted
- **Date:** 2026-07-14

## Context

ADR-0009 committed to a self-hosted VPS running Docker Compose, generated via
`aspire publish` and (implicitly) run by hand. A draft ADR-0013 additionally planned to
put Cloudflare in front of that VPS, with TLS handled by manually installing and
rotating a Cloudflare Origin CA certificate. While registering the production domain
(settlapp.se) we discovered the actual target VPS already runs
[Dokploy](https://dokploy.com/), a self-hosted PaaS that manages its own Traefik
reverse proxy, its own TLS certificates, and a native Postgres database service with
scheduled backups — none of which either prior ADR accounted for. A `/grill-with-docs`
session (this ADR) researched Dokploy's actual documented behavior before deciding how
to reconcile.

## Decision

We will deploy Settl as a Dokploy-managed Docker Compose project, built from the same
Aspire-generated compose file and Dockerfile that ADR-0009 already produces — Dokploy
runs `docker compose` under the hood and layers in its own domain/TLS routing via its
dashboard, so ADR-0009's build/image story is unchanged; only who runs `docker compose
up` changes. Postgres runs as Dokploy's native Database service rather than inside
Settl's own compose stack, so it gets Dokploy's built-in scheduled `pg_dump` backups to
S3-compatible storage. Cloudflare proxies settlapp.se in Full (Strict) mode using
Dokploy's automatically-provisioned Let's Encrypt certificate — Dokploy's own officially
documented, supported configuration — rather than a manually-managed Cloudflare Origin
CA certificate: Dokploy only supports Let's Encrypt natively, and hand-editing Traefik
for an Origin CA cert is a documented, currently-unresolved pain point that breaks
across Dokploy updates ([dokploy/dokploy#1839](https://github.com/Dokploy/dokploy/issues/1839)).
The VPS firewall still restricts inbound 80/443 to Cloudflare's published IP ranges,
unchanged from ADR-0013's draft — this doesn't conflict with Dokploy/Traefik, which
still needs 80/443 reachable (via Cloudflare) for routing and the Let's Encrypt
HTTP-01 challenge. This ADR supersedes draft ADR-0013.

## Consequences

Settl's connection string now points at an externally-addressed (Dokploy-managed)
Postgres instance rather than a compose-internal one — a small, mechanical config
difference from what ADR-0010 silently assumed, not an architecture rewrite. This
resolves tech-debt/0005 (no off-box backup) as a decision, the same way ADR-0011
resolved tech-debt/0001 before the sending code existed — the actual migration to a
Dokploy-managed Postgres instance still needs doing. Cert rotation ceremony is
eliminated entirely: Dokploy/Traefik auto-renews Let's Encrypt certs, so there's
nothing to manually install or rotate on the box, a real simplification over ADR-0013's
original plan. Dokploy itself becomes a new operational dependency — it must be kept
patched (a security fix was required as recently as v0.29.3 per its
[release history](https://github.com/Dokploy/dokploy/releases)), and its own
dashboard/auth becomes something to secure in its own right. The site currently running
on the VPS under Dokploy needs to be removed there before Settl can be deployed — an
operational step, not an architectural one. Revisit if the Dokploy project is
abandoned/unmaintained, or if Settl's traffic ever needs multi-VPS scale-out a single
Dokploy instance can't manage cleanly (the same trigger ADR-0009 already named for
revisiting the single-VPS approach).

## Sources

- [Dokploy: Cloudflare domain guide](https://docs.dokploy.com/docs/core/domains/cloudflare) — Full (Strict) + Let's Encrypt is the documented, supported combination
- [Dokploy: Cloudflare Tunnels guide](https://docs.dokploy.com/docs/core/guides/cloudflare-tunnels) — considered, not chosen (more moving parts for a benefit the firewall IP-allowlist already covers)
- [Dokploy: Databases](https://docs.dokploy.com/docs/core/databases) / [Backups](https://docs.dokploy.com/docs/core/backups) — native Postgres service with scheduled S3-compatible backups
- [Dokploy: Docker Compose domains](https://docs.dokploy.com/docs/core/docker-compose/domains) — native domain/TLS routing without hand-written Traefik labels
- [dokploy/dokploy#1839](https://github.com/Dokploy/dokploy/issues/1839) — Origin CA certificate support still unresolved as of this writing
- [Dokploy GitHub releases](https://github.com/Dokploy/dokploy/releases) — active maintenance, recent security patch cadence
