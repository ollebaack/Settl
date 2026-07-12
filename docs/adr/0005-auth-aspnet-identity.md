# ADR-0005: Self-hosted auth with ASP.NET Identity

- **Status:** accepted
- **Date:** 2026-07-12

## Context

Settl needs accounts, household invites, and (later) token auth for a mobile app.
Hosted auth (Clerk/Auth0) would be faster to good UX; self-hosting keeps zero vendors
and uses existing .NET knowledge.

## Decision

We will use ASP.NET Identity, self-hosted, with a token flow the future Expo app can use.

## Consequences

We own password reset, invite links, and therefore **email delivery** — a real cost
accepted knowingly (tracked in tech-debt). No vendor lock-in, no per-user pricing.
Implementation details (cookie vs JWT, invite flow) deserve their own grill session
before the auth feature is built; this ADR only fixes the "self-hosted Identity" choice.
