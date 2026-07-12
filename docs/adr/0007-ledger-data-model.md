# ADR-0007: Ledger data model

- **Status:** accepted
- **Date:** 2026-07-12

## Context

The design exports (`docs/design/`) fix entry types, split modes, recurrence UX, and
nudge triggers, but leave the persistence model contested — notably the prototype
(`settled` boolean + `paidPairs`) contradicts the handoff (`settledPairs`, "not a
boolean"). Grilled 2026-07-12.

## Decision

- **Entries** are `expense | iou | recurring-post`. Recurring costs are a separate
  template entity; each cycle posts a real entry linking back to its template.
- **Settlements are first-class events** (who, with whom, when, which entries closed).
  Entry/pair settled state is *derived* from settlement records, never stored as a flag.
- **Splits store both** the formula (`equal | percent | amount` + values) and frozen
  per-person integer shares, computed once at write time with deterministic remainder
  distribution.
- **Recurrence posting** runs in a hosted background service with catch-up on startup;
  the posting logic itself is pure, unit-testable functions.
- **Nudges are derived on read** from the three design triggers (recurring due ≤5 days,
  large expense, balance threshold). No notification storage in v1.
- **Entries are editable/deletable until a settlement touches them**, then locked
  (reopen first). Notebook, not bookkeeping journal.
- **Households carry an ISO currency** (default `SEK`), no picker in v1. Amounts are
  integer minor units everywhere.

## Consequences

More model than the handoff sketch (Settlement entity, shares rows) in exchange for:
settlement history and undo without migration, history immune to membership changes,
rounding decided exactly once. Read endpoints stay pure — posting is the only
time-driven write path. Push notifications later require persisting delivered
notifications (tech-debt 0002). No reversal-style audit trail — accepted, fits the
open trust model.
