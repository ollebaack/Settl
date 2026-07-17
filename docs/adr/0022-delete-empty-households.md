# ADR-0022: Hard-delete of empty households

- **Status:** accepted
- **Date:** 2026-07-17
- **Amends:** [ADR-0016](0016-household-ownership-and-archival.md) (soft archive, not hard delete)

Feature shape lives in the spec:
[`docs/specs/household-ownership.md`](../specs/household-ownership.md). This ADR records
only the load-bearing decision and its boundary. Grilled 2026-07-17.

## Context

ADR-0016 established soft archive as the only removal path and rejected hard delete
because it is "irreversible for shared financial data" and "a misclick must be
recoverable." But households are also created by mistake, and an archived mistake
lingers in the "Arkiverade" list forever with no way to truly remove it. The ADR-0016
rejection rests entirely on protecting *financial history* — an **empty** household
(no entries, recurring templates, or settlements) has none, and is trivially re-created.

## Decision

We will allow the **owner** to **hard-delete a household only when it has no ledger
activity** — no entries, no recurring templates, no settlements. Deletion cascades away
memberships and pending invites. It is available directly on the active household (no
archive-first step), and where other members remain it **warns but never blocks**,
consistent with ADR-0016's "warn, never block" rule for leave/archive. The empty-check
is re-evaluated inside the delete transaction so activity added concurrently returns 409
rather than being silently destroyed.

## Consequences

The household record (name/identity) is irreversibly gone, accepted because it carries no
history and is cheap to recreate. Other members can lose an empty shared household with
only a warning — the one case where "empty" might mean "not yet used" rather than
"mistake." This does **not** reopen hard-delete for households with financial data:
ADR-0016 still governs everything non-empty, and any future GDPR/storage purge of
*non-empty* households remains a separate decision. Revisit if users report losing
households they were still setting up together.

## Rejected alternatives

- **Delete silently when others are members** — a joined member losing a shared
  household with zero signal; violates ADR-0016's warn-first principle.
- **Block delete when others are members** — traps trivially-empty households; owner
  should be able to clean up a mistake.
- **Archive-first before delete** — unnecessary friction for removing a misclick.
- **Pending invites block deletion** — an outstanding invite is not financial data;
  it is cascade-revoked instead.
