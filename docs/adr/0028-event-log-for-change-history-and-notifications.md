# ADR-0028: Append-only event log for change history, notifications, and audit

- **Status:** accepted
- **Date:** 2026-07-18

## Context

Any household member can silently change the ledger in a way that shifts what you owe
or are owed — delete a row, edit an amount/payer/split, mark a debt paid, or change a
recurring cost. Today none of this is recorded: entry deletion is a hard delete
([EntriesEndpoints.cs:146](../../apps/api/Settl.Api/Features/EntriesEndpoints.cs)),
`Entry` has no creator or change history ([Entities.cs:72](../../apps/api/Settl.Api/Domain/Entities.cs)),
and the "Notiser" screen ([activity.tsx](../../apps/web/src/routes/activity.tsx)) shows
*nudges* that are derived live from current balances — structurally unable to report a
deletion (nothing left to derive from) or an edit's before/after. We need a mechanism to
tell an affected member "someone changed this" so they can't be quietly cheated. Decided
via `/grill` on 2026-07-18; the feature shape lives in
[trust-notifications-v1](../specs/trust-notifications-v1.md).

## Decision

We will introduce an **append-only event log**: one immutable row per state-changing
domain action, carrying actor member id, event type, household id, a denormalized
reference and a **structured before/after payload** of the changed fields, plus a UTC
timestamp. In-app notifications are **projected** from this log — not stored per
recipient — and unread state is a per-member "last seen" cursor. The log is written for
mutations regardless of whether a notification is shown, so it doubles as an audit and a
future restore source. It has **no foreign key to the entities it references** (a
referenced entry may be hard-deleted); the denormalized snapshot must stand alone.

## Consequences

- Every mutating handler (create/edit/delete/settle/reopen/recurring change/membership)
  must emit an event in the same transaction as the change. Forgetting to emit one is a
  silent gap — this becomes a review checkpoint.
- Rejected **soft-delete + per-entity change columns**: solves deletion only, captures
  edits' before/after poorly, and scatters logic across entities. Rejected **extending
  derived nudges**: cannot detect deletes or edits at all.
- Structured payload (vs a frozen rendered string) costs a per-event-type schema, but
  buys machine-readable diffs, copy that can change without rewriting history, and a path
  to "restore deleted row" later.
- Actor tracking generalizes the existing `SettlementClosures.InitiatedByMemberId`
  precedent to all mutations; `Entry` still needs no `CreatedBy` — the log carries it.
- The log grows unbounded and holds denormalized financial detail; retention/pruning and
  its privacy surface are deferred, and revisited if table size or GDPR concerns bite.
- Projecting notifications keeps writes cheap (no fan-out) but means "dismiss one notice"
  isn't possible under the cursor model — accepted for v1.
