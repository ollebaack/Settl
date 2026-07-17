# ADR-0023: Balance nudge fires on threshold crossing, derived from the event log

- **Status:** accepted
- **Date:** 2026-07-17

## Context

The balance nudge fires on every read while `abs(pairwise net) >= 750 kr`
(`NudgeCalculator`), so it nags continuously instead of "only speaking up when
something happens" (product brief). ADR-0007 deferred true crossing detection on the
assumption it "needs prior-state tracking [storage]"; the implementation map flagged
this as ambiguity #6. But the pair's balance timeline is already fully derivable from
existing timestamps — `Entry.CreatedAt` and `Settlement.SettledAt` — so no new storage
is required. Grilled 2026-07-17.

## Decision

We will emit the balance nudge only when the pair's net **crosses** `750 kr`, detected
by replaying the pair's net chronologically over `Entry.CreatedAt` / `Settlement.SettledAt`
events (no accounting `Date`, so backdating can't spoof freshness). A nudge is shown
only if the most recent upward crossing (`|net|` going from `< 750` to `>= 750`) occurred
within **7 days** (the existing big-expense window). No nudge is stored — this upholds
ADR-0007's "derived on read, no notification storage" rather than amending it. The
crossing/timeline math lives in `BalanceCalculator` (unit-tested per API rules);
`NudgeCalculator` stays pure and receives the precomputed crossing timestamp.

## Consequences

Settling below the threshold and climbing back over, or a sign flip through zero, both
re-fire naturally — the replay dips below `750` first, then re-crosses. A standing debt
older than 7 days shows no nudge (accepted: it's no longer news; the balance is still
visible on Home). Read cost rises by a small in-memory replay per pair over
already-loaded entries/closures — negligible at household scale. Rejected: a
high-water-mark table per pair (forces write-on-read or couples nudge state to
mutations) and an emitted-nudge log (that is tech-debt 0002, justified only once real
push/email delivery exists). Revisit if we add out-of-app delivery, which needs
persisted, de-duplicated notifications.
