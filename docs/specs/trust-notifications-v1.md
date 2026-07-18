# Trust notifications (v1) — spec

Notify a household member when **someone else** changes something that affects what they
owe or are owed — so no one can be quietly cheated. Built on an append-only event log
([ADR-0028](../adr/0028-event-log-for-change-history-and-notifications.md)); the in-app
"Notiser" screen grows from a live-derived nudge list into a persistent stream of "this
happened to your money". This spec is the feature shape; ADR-0028 is the load-bearing
mechanism decision.

Provenance: decided via `/grill` on 2026-07-18. Companion ADR-0028 covers the event-log
mechanism (hard to reverse); this spec covers scope, recipients, and surfaces.

## Problem

A member can shift the ledger against you with no trace or notice: entry deletion is a
hard delete ([EntriesEndpoints.cs:146](../../apps/api/Settl.Api/Features/EntriesEndpoints.cs)),
edits overwrite in place ([EntriesEndpoints.cs:98](../../apps/api/Settl.Api/Features/EntriesEndpoints.cs)),
and today's nudges ([activity.tsx](../../apps/web/src/routes/activity.tsx),
[NudgeCalculator.cs](../../apps/api/Settl.Api/Domain/NudgeCalculator.cs)) are recomputed
from current state, so they cannot say "X changed this." Separately, `DELETE /entries/{id}`
is the only entry endpoint with no `ICurrentUserAccessor` — any authenticated user can
delete any entry by GUID, with no household-membership check. That is a bug, fixed as a
prerequisite (not part of the notifications feature).

## Scope (v1)

High-risk triggers only — where money quietly leaks. Each emits one event:

- **Entry deleted** — captures the deleted entry's snapshot before removal.
- **Entry edited** — amount, payer, or split changed (before/after per field).
- **Settlement recorded / debt marked paid** — a closure created against you.
- **Recurring cost changed** — amount or schedule of a recurring cost you're on.

Recipient rule: an event reaches every member who is **financially party** to it (on the
split, the payer, or debtor/creditor), **except the actor** — you are never notified of
your own change. Unread = events after your per-member "last seen" cursor that concern
you.

**Deletion policy:** any household member may delete any entry — **not** creator-only.
The safeguard is not restriction but transparency: a confirmation prompt before the act
(already present — confirm dialog + 5 s "Ångra" undo in
[use-entry-delete.ts](../../apps/web/src/lib/use-entry-delete.ts)) and an `EntryDeleted`
event so the affected members are told who removed it. The confirmation copy should make
clear when the row being deleted belongs to someone else.

Deliberately **not** in v1 (mechanism supports adding them later without rework): new
entry that charges you, being added to a split, reopened settlement, membership changes,
email/push delivery, and per-notice dismiss.

## Data model

- **`LedgerEvents`** (new, append-only): `Id`, `HouseholdId`, `ActorMemberId`,
  `Type` (enum: EntryDeleted, EntryEdited, SettlementRecorded, RecurringChanged),
  `EntryId?`/`RecurringId?` (denormalized reference, **no FK** — target may be deleted),
  `PayloadJson` (structured before/after of changed fields + denormalized title),
  `OccurredAt` (UTC). Indexed on `(HouseholdId, OccurredAt)`.
- **`Member.NotificationsSeenAt`** (new, nullable UTC): the read cursor.
- No change to `Entry` — creator/actor lives on the event, not the entity.
- Migration adds both; no backfill (history starts at deploy).

## API surface

- **Emit on mutation:** the delete/edit/settle/recurring-change handlers write a
  `LedgerEvent` in the same transaction as the change.
- **`GET /households/{id}/notifications`** — events concerning the caller, newest first,
  each with an `isUnread` flag computed against their cursor; plus an unread count.
- **`POST /households/{id}/notifications/seen`** — advances the caller's cursor to now.
- **Prerequisite fix:** `DELETE /entries/{id}` gains `ICurrentUserAccessor` + a
  household-membership check, matching the other entry endpoints. This is a
  membership check **only** — deletion stays open to any member of the household, by
  design (see Deletion policy); the check just blocks non-members.
- api-client regenerated in the same PR (root rule).

## Web surface

- The "Notiser" screen ([activity.tsx](../../apps/web/src/routes/activity.tsx)) renders
  the notifications stream with unread affordance; opening it advances the cursor. Design
  export to be produced via the `ui-design` skill before build.
- Nudges (advisory reminders) and trust events (things that happened) coexist — the spec
  keeps them visually distinguishable rather than merging the two concepts.

## Out of scope / open questions

- Email/push delivery of trust events — a later step can reuse NudgeDigest infrastructure
  ([NudgeDigestService.cs](../../apps/api/Settl.Api/Services/NudgeDigestService.cs)); push
  is its own grill (PWA landed in #77).
- "Restore deleted row" from the event snapshot — the log makes it possible; not built.
- Retention/pruning of `LedgerEvents` and its privacy/GDPR surface.
- Per-notice dismiss (the cursor model is all-or-nothing).
- The remaining triggers listed under "not in v1".
