# Ledger editing affordances — spec

Make ledger entries and recurring templates safely editable and removable from the UI:
fix the "Du betalade" mislabel, let one person owe the whole amount, add a safe delete with
undo, and let recurring templates be cleaned up. The load-bearing decisions and rejected
alternatives (ADR-0018, grilled 2026-07-16) are in the
[Decision record](#decision-record-adr-0018) below.

Provenance: decided via `/grill` on 2026-07-16 (was ADR-0018). Investigation found the
server affordances mostly already existed (edit `PUT /entries/{id}`, hard delete
`DELETE /entries/{id}` with a 409 settlement lock per ADR-0007, recurring deactivate); the
gap was missing/unsafe UI plus two small model questions — settled below.

## Problem

Product wishlist items 6–8: "Du betalade" shows even when the adder only logged the entry
(didn't pay); entries can't be undone/edited from the UI; and recurring posts can't be
removed. The underlying endpoints exist, so this is mostly UI plus deciding whether the model
needs to change to support them.

## Scope (v1)

- **Payer/split is UI-only — no model change.** Keep the payer default; add an expense
  **split preset for "one person owes the whole amount"** (payer share = 0), kept inside the
  expense flow. Do **not** add `CreatedByMemberId`; do **not** make the payer nullable.
- **Entry delete stays HARD** (ADR-0007 holds). Safety is a confirmation dialog plus a
  client-side ~5 s "Ångra" undo toast that defers the DELETE. Undo covers delete only; a
  mis-edit is recovered by editing again. No server soft-delete/tombstone.
- **Recurring templates gain delete-if-clean, else deactivate.** A template that has posted
  zero entries can be hard-deleted (new `DELETE /recurring/{id}` that 409s when posted entries
  reference it); once it has posted history it can only be deactivated (`Active=false`),
  preserving the real debts its cycles created. Already-posted recurring entries are ordinary
  entries, deleted via the normal entry delete + undo.

## API surface

- Reuses `PUT /entries/{id}` and `DELETE /entries/{id}` (already present; the delete 409s when
  a settlement locks the entry, ADR-0007).
- New `DELETE /recurring/{id}` — deletes a template with zero posted entries, 409 otherwise.
- Regenerate `packages/api-client` if the recurring shape changes (root rule).

## Web surface

- Add-entry expense flow: the `Allt` split preset (payer share = 0). See
  [unified-add-entry.md](unified-add-entry.md), which extends this preset.
- Entry delete: confirmation dialog + `Ångra` undo toast (`use-entry-delete.ts`).
- Recurring detail: delete-if-clean vs deactivate affordance.

## Decision record (ADR-0018)

`CreatedByMemberId` and a nullable payer were **rejected**: both reintroduce audit-trail
state ADR-0007 deliberately declined, and neither is what the user asked for — the real gap
was split flexibility. The `Allt` split preset overlaps conceptually with the old Lån (IOU),
but balances won't double-count (one expense, one zero share); ADR-0020 later removed the IOU
type entirely, making `Allt` the sole "one owes all" path.

Hard delete still means one member's delete silently moves everyone's balance. The undo toast
protects only the deleter, only on their device, and is best-effort (tech-debt/0010).
Soft-delete stays rejected under the open-trust, notebook-not-bookkeeping model. **Revisit**
if households report surprise balance changes from another member's delete, or if cross-user
delete visibility/recovery becomes a requirement — that would introduce soft-delete records
and supersede this decision and the relevant part of ADR-0007. (The transparency angle was
later addressed by the event log / trust notifications, ADR-0028, without reversing hard
delete.)

## Out of scope / open questions

- Cross-user delete recovery / soft-delete — rejected here; see the revisit trigger above.
- Recurring end-dates and scheduling changes — separate spec
  ([recurring-end-date.md](recurring-end-date.md)).
