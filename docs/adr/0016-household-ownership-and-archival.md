# ADR-0016: Household ownership and archival

- **Status:** accepted
- **Date:** 2026-07-16

## Context

Households are shared, many-to-many, and today have no way to be removed — no
`DELETE` endpoint, no creator recorded, and flat membership (`HouseholdMembership`
has `JoinedAt`, no role). Users want to leave groups they no longer use and remove
groups they own, but "delete" on a shared ledger destroys other people's financial
history, and in a debt-tracking app it must not become a way to escape open debts.
Grilled 2026-07-16.

## Decision

- **Introduce a single owner per household.** Add an owner reference on `Household`.
  New households: the creator is the owner (we start recording it). Existing
  households: backfill owner to the member with the earliest `JoinedAt` (proxy —
  creator was never stored).
- **Ownership is fixed until the owner leaves, and transfer is explicit.** The owner
  cannot leave until they transfer ownership to another member. Transfer is what
  makes someone the new owner — it never happens automatically.
- **Only the owner can archive the whole household**, even while other members remain.
  Non-owners can only **Leave** (remove their own membership).
- **"Remove" means soft archive, not hard delete.** An archived household is hidden
  from all members but all data is retained and can be **restored by the owner**.
  Archived households appear in a dedicated "Archived" section, not support-only.
  No cascade delete, no automatic purge.
- **Open debts never block the action, but always warn.** Both Leave and Archive show
  the outstanding amount ("you/others have X kr open") and require confirmation.
- **Sole owner leaving:** with no one to transfer to, the action archives the
  household.

## Consequences

New role/owner column plus a one-time backfill migration; the API shape changes
(regenerate `packages/api-client`) with new endpoints (archive, restore, leave,
transfer ownership). We accept that the owner can archive a household others still
use — destructive-for-all power, deliberately gated to the owner plus a debt warning.
Soft-only archival keeps all data indefinitely: GDPR and storage cleanup are deferred
and would trigger a follow-up purge decision. We deliberately **do not** extend
ADR-0007's hard-delete stance for individual entries to whole households — a shared
ledger is a bigger unit and a misclick must be recoverable.

Explicitly rejected: co-owners (more role management; any owner could destroy),
auto-transfer to the oldest member (makes someone owner without choosing), hard delete
(irreversible for shared data), and blocking removal until debts net to zero (can trap
members in dead households when someone refuses to settle).
