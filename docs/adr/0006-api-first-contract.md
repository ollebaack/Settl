# ADR-0006: The API is the durable contract; UIs are disposable

- **Status:** accepted
- **Date:** 2026-07-12

## Context

The web UI will likely be rebuilt in Expo for iOS (ADR-0002). Whatever we build twice
must be cheap; whatever we build once must be solid.

## Decision

All business rules (splitting math, recurrence, settlement, balances, reminder triggers)
live server-side. The OpenAPI spec is the contract; `packages/api-client` holds
TypeScript types generated from it, consumed by every frontend. Frontends render and
call — they do not compute.

## Consequences

Slightly more API ceremony (endpoints for things a SPA could compute locally). In
exchange, the Expo app inherits correct behavior for free, and split/balance math has
one implementation to test. Regenerating the client is part of any API-shape change —
same PR, or CI should catch the drift.
