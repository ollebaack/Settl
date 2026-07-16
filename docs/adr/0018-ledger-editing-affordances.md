# ADR-0018: Ledger editing affordances (edit, delete, recurring cleanup)

- **Status:** accepted
- **Date:** 2026-07-16

## Context

Product wishlist items 6–8 reported that "Du betalade" shows even when the adder
only logged the entry, that entries can't be undone/edited, and that recurring
posts can't be removed. Investigation found edit (`PUT /entries/{id}`) and delete
(`DELETE /entries/{id}`, HARD, 409 when a settlement locks it) already exist per
ADR-0007, and recurring templates already deactivate (`Active=false`) rather than
delete. So the cluster is mostly missing/unsafe UI plus two small model questions.
Grilled 2026-07-16.

## Decision

- **Payer/split is UI-only — no model change.** We keep the payer default and add an
  expense **split preset** for "one person owes the whole amount" (payer share = 0),
  kept inside the expense flow. We will NOT add `CreatedByMemberId` and will NOT make
  the payer nullable.
- **Entry delete stays HARD (ADR-0007 holds).** Safety is a confirmation dialog plus
  a client-side ~5 s "Ångra" undo toast that defers the DELETE. Undo covers delete
  only; a mis-edit is recovered by editing again. We will NOT add a server
  soft-delete/tombstone.
- **Recurring templates gain delete-if-clean, else deactivate.** A template that has
  posted zero entries can be hard-deleted; once it has posted history it can only be
  deactivated, preserving the real debts its cycles created. Already-posted recurring
  entries are ordinary entries, deleted via the normal entry delete + undo.

## Consequences

`CreatedByMemberId` and a nullable payer were rejected: both reintroduce audit-trail
state ADR-0007 deliberately declined, and neither is what the user asked for — the
real gap was split flexibility. The split preset overlaps conceptually with Lån (IOU);
balances must not double-count, which they won't (one expense, one zero share).

Hard delete still means one member's delete silently moves everyone's balance. The
undo toast protects only the deleter, only on their device, and is best-effort — see
tech-debt 0010. Soft-delete stays rejected under the open-trust, notebook-not-
bookkeeping model. **Revisit** if households report surprise balance changes from
another member's delete, or if cross-user delete visibility/recovery becomes a
requirement — that would introduce soft-delete records and supersede this decision
and the relevant part of ADR-0007.

Delete-if-clean adds one branch to the recurring delete path (a new
`DELETE /recurring/{id}` that 409s when posted entries reference the template).
