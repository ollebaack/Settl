# ADR-0013: Cloudflare as DNS + edge proxy in front of the production VPS

- **Status:** superseded by ADR-0014
- **Date:** 2026-07-14

## Context

ADR-0009/0010 committed to a single self-hosted VPS running one Docker Compose stack
(API, Postgres, and the SPA all self-hosted) — that topology isn't changing. The domain
is already on Cloudflare, so the question is only what role Cloudflare plays: nothing
(plain DNS), or DNS plus the orange-cloud edge proxy in front of the VPS for free
TLS/CDN/WAF/DDoS protection. Cloudflare can't run .NET or self-host Postgres, so hosting
any part of the app itself on Cloudflare (Workers/Pages/D1) was considered and rejected —
that would mean rewriting the API and splitting off the SPA for a benefit (edge caching)
that's speculative pre-launch, not a hosting swap.

## Decision

We will point the domain's nameservers at Cloudflare and proxy production traffic
through it (orange-cloud on) in front of the single VPS from ADR-0009. TLS between
Cloudflare and the origin runs in Full (strict) mode using a Cloudflare Origin CA
certificate on the VPS — never Flexible, since that would leave the Cloudflare→VPS hop
unencrypted for an app handling household financial data. The VPS firewall is locked
down to accept inbound 80/443 only from Cloudflare's published IP ranges, so the
WAF/DDoS protection can't be bypassed by hitting the VPS's IP directly.

## Consequences

Free DDoS/WAF/CDN protection and TLS termination at the edge, with no change to the
API/Postgres/SPA architecture — ADR-0009 and ADR-0010 stand as-is. In exchange we take
on Cloudflare as a new operational dependency: the origin firewall rule needs occasional
re-sync when Cloudflare updates its IP ranges, and the Origin CA cert needs to be
installed and rotated on the VPS. If Cloudflare's IP list ever changes without the
firewall being updated, production traffic could break; if the firewall is ever
loosened, the direct-IP bypass risk returns silently. Revisit if the SPA is later split
into its own container (ADR-0009's named escape hatch) and edge caching becomes worth
configuring per-route, or if Cloudflare's free tier stops covering actual traffic/WAF
needs.
