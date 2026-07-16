# ADR-0016: Household ownership and archival

- **Status:** accepted
- **Date:** 2026-07-16

The full feature (roles, flows, endpoints, states, migration) lives in the spec:
[`docs/specs/household-ownership.md`](../specs/household-ownership.md). This ADR records
only the load-bearing decisions and the alternatives we rejected. Grilled 2026-07-16.

## Context

Households are shared and many-to-many, and today cannot be removed — no `DELETE`
endpoint, no owner recorded, flat membership with no role. Users want to leave groups
they no longer use and remove groups they own, but on a shared ledger "delete" destroys
other people's financial history, and in a debt-tracking app removal must not become a
way to escape open debts.

## Decisions

- **Single owner per household, not co-owners.** One `OwnerMemberId`; the creator owns
  new households, and existing ones backfill to the earliest-joined member.
- **Soft archive, not hard delete.** "Remove" hides the household and retains all data;
  the owner can restore it indefinitely. No cascade delete, no automatic purge.
- **Transfer before leave, never auto-transfer.** The owner must explicitly hand
  ownership to a chosen member before leaving; ownership never moves on its own. A sole
  owner leaving archives the household instead.
- **Warn on open debts, never block.** Leave and archive show the outstanding amounts
  and require confirmation, but always proceed — debts don't trap members.

## Consequences

New owner + archival columns and a one-time backfill migration; new endpoints (transfer,
leave, archive, restore) so the API shape changes (regenerate `packages/api-client`). We
accept that the owner can archive a household others still use — gated to the owner plus
a debt warning. Soft-only archival keeps all data indefinitely: GDPR and storage cleanup
are deferred and would trigger a follow-up purge decision. We deliberately do **not**
extend ADR-0007's hard-delete stance for individual entries to whole households — a
shared ledger is a bigger unit and a misclick must be recoverable.

## Rejected alternatives

- **Co-owners** — more role management, and any owner could destroy the household.
- **Auto-transfer to the oldest member** — makes someone an owner without choosing.
- **Hard delete** — irreversible for shared financial data.
- **Block removal until debts net to zero** — traps members in dead households when
  someone refuses to settle.
