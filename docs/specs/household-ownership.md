# Household ownership, leaving & archival — spec

What we're building: a way to **own**, **leave**, and **remove** a household without
destroying other people's financial history, and without letting anyone escape open
debts by disappearing. Decision record (grill outcome + rejected alternatives):
[ADR-0016](../adr/0016-household-ownership-and-archival.md). Visual reference:
[`docs/design/Settl Household Management.dc.html`](../design/Settl%20Household%20Management.dc.html)
(mint theme, Swedish copy — **household = rounded square, person = circle**).

## Why

Households are shared and many-to-many (a person is in several; a household has several
people). Today there is no way to remove one: no `DELETE` endpoint, no owner recorded,
and flat membership (`HouseholdMembership` = `HouseholdId` + `MemberId` + `JoinedAt`, no
role). Users want to **leave** groups they no longer use and **remove** groups they run.
But on a shared ledger "delete" would erase everyone else's history, and in a
debt-tracking app removal must not become a loophole to walk away from what you owe.

## Goals

- A household has exactly one **owner**; ownership is explicit and transferable.
- Any member can **leave**; the owner can **archive** (soft) and **restore**.
- A household with financial history is never hard-deleted — a misclick on a whole shared
  ledger must be recoverable (soft archive only).
- An **empty** household (no ledger activity) can be **hard-deleted** by the owner, since
  there is no history to protect — for cleaning up mistakes ([ADR-0020](../adr/0020-delete-empty-households.md)).
- Open debts are always surfaced as a warning, but never block leaving or archiving.

## The owner role

- Each `Household` has a single `OwnerMemberId` (always one of its current members).
- **New households:** the creator becomes the owner (we start recording it — today the
  creator is not stored at all).
- **Existing households (backfill):** owner = the member with the earliest `JoinedAt`
  (tie-break by `MemberId`), i.e. the first entry in the canonical `MembershipOrder`.
  This is a proxy — the real creator was never persisted.
- Only the owner can rename, archive, restore, delete (when empty), or transfer
  ownership of the household. Non-owners can only leave.

## States

A household is either **active** (`ArchivedAt == null`) or **archived**
(`ArchivedAt` set). Archival is soft: every row (memberships, entries, settlements,
templates, invites) is retained. There is no automatic purge, and archival never cascades.

- Active households appear in the normal household list.
- Archived households are hidden from the normal list and shown in a dedicated
  **"Arkiverade"** section. Only the owner sees the **Restore** affordance.

**Delete** is a separate, terminal path from archive (not a state): the owner can
permanently remove a household **only when it is empty** (no entries, recurring
templates, or settlements). This cascade-deletes memberships and pending invites. See
[Delete household](#delete-household-owner-only-empty-only--adr-0020).

## Flows

Copy and layout come from the design export; the frame numbers below map to it.

### Transfer ownership (owner → member) — frame 4

- Owner picks another current member; that member becomes the owner and the previous
  owner becomes an ordinary member. Never automatic — transfer is the only way ownership
  moves while the owner stays in the household.

### Leave household — frames 2, 3, 6

- **Non-owner leaves:** their membership row is removed; the household stays active for
  everyone else. Open debts warn (frame 3, per-person: "Du är skyldig Sam 320 kr",
  "Priya är skyldig dig 150 kr") but do not block.
- **Owner leaves with other members present:** blocked. They must **transfer ownership
  first** (frame 1 note: "För att lämna — överför ägarskapet först").
- **Sole owner leaves** (no one to transfer to) — frame 6: leaving **archives** the
  household. The member keeps their membership and ownership so they can restore it
  later; the household simply moves to their "Arkiverade" section. Button reads "Lämna
  och arkivera".

### Archive household (owner only, soft) — frame 5

- Only the owner can archive, even while other members remain — deliberately
  destructive-for-all power, gated to the owner plus a warning. The sheet shows how many
  members are affected and the household's **total** outstanding debt ("Ni har 1 240 kr
  i öppna skulder kvar"). Confirmation required; debts never block.

### Restore household (owner only) — frame 7

- The owner restores from the "Arkiverade" section; `ArchivedAt` is cleared and the
  household returns to the normal list. Restorable indefinitely.

### Delete household (owner only, empty only) — ADR-0020

- Only the owner can delete, and only when the household is **empty**: no entries, no
  recurring templates, no settlements. Pending invites do **not** count as activity —
  they are cascade-revoked on delete.
- Available directly on the active household (no archive-first step). The affordance is
  present but **disabled** while the household has activity; the owner archives instead.
- When other members remain, the confirmation **warns** ("N andra medlemmar förlorar
  åtkomst") and requires confirmation, but never blocks — consistent with archive.
- Deletion is **permanent and cascades**: the household row, all `HouseholdMembership`
  rows, and household-scoped pending `Invite`s are removed. It is irreversible; there is
  no restore. (Nothing financial exists to lose — that is the precondition.)
- The empty-check is re-evaluated inside the delete transaction: if activity was added
  concurrently, the delete fails with 409 rather than destroying it.

## Edge cases & rules (enforced server-side)

| Case | Rule |
| --- | --- |
| Transfer to a non-member | 400 — target must be a current member. |
| Transfer to self | 400 — no-op. |
| Transfer by a non-owner | 403. |
| Owner leaves, others remain | 409 — must transfer ownership first. |
| Sole member (always the owner) leaves | Archives household; membership + ownership kept. |
| Archive / restore by a non-owner | 403. |
| Archive an already-archived household | 409. |
| Restore a non-archived household | 409. |
| Delete by a non-owner | 403. |
| Delete a household with any entry, template, or settlement | 409 — not empty. |
| Delete with pending invites | Allowed; invites cascade-revoked (not activity). |
| Delete with other members present | Allowed; warn "N andra medlemmar förlorar åtkomst", never block. |
| Open debts on leave / archive | Warn with amounts; never block. |
| Any of the above on a household you're not in | 404 (don't leak existence). |

Owner is always a current member, so "sole member" implies "sole owner" — no orphaned
household is ever produced.

## API surface

New fields on the household DTOs so the client can render ownership and the archived
section:

- `HouseholdDto` / `HouseholdListItemDto` gain `ownerMemberId: Guid`, `isOwner: bool`,
  and `archivedAt: string | null` (UTC).
- `GET /households` returns **active only** by default; `?includeArchived=true` also
  returns archived households (for the "Arkiverade" section).

New endpoints (all scoped to `/households/{id}`, all require the caller to be a member —
otherwise 404):

| Method + path | Who | Effect |
| --- | --- | --- |
| `POST /households/{id}/transfer-ownership` | owner | Body `{ newOwnerMemberId }`. Reassigns owner. |
| `POST /households/{id}/leave` | any member | Non-owner: removes membership. Sole owner: archives, keeps membership. Owner with others: 409. |
| `POST /households/{id}/archive` | owner | Sets `ArchivedAt`. 409 if already archived. |
| `POST /households/{id}/restore` | owner | Clears `ArchivedAt`. 409 if not archived. |
| `DELETE /households/{id}` | owner | Permanently deletes an **empty** household (cascades memberships + pending invites). 409 if it has any entry, template, or settlement. 204 on success. |
| `GET /households/{id}/removal-preview` | any member | Debt figures + guard flags for the leave/archive/delete sheets (below). |

### Removal preview

`GET /households/{id}/removal-preview` gives the client everything the confirmation
sheets need in one call, computed from the pure
[`BalanceCalculator`](../../apps/api/Settl.Api/Domain/BalanceCalculator.cs):

- `isOwner`, `memberCount`, `soleMember` (caller is the only member → leave archives),
  `mustTransferFirst` (owner with other members → leave blocked).
- `isEmpty` (no entries, recurring templates, or settlements → owner may hard-delete),
  so the client can enable/disable the delete affordance without a second call.
- `viewerOpenDebts`: per-other-member net for the caller (`memberId`, `name`,
  `avatarColor`, `netMinor`, `relation` — same shape as the summary's people), for the
  **leave** sheet (frame 3).
- `householdOpenTotalMinor`: sum of all open debt amounts across the household, for the
  **archive** sheet (frame 5).

Regenerate `packages/api-client` in the same PR (root rule).

## Data model & migration

- `Household.OwnerMemberId` (`Guid`, non-null) and `Household.ArchivedAt`
  (`DateTimeOffset?`, null = active).
- EF migration (Npgsql — dev/prod are Postgres per ADR-0010; tests build the schema from
  the model via `EnsureCreated`): add `OwnerMemberId` nullable, backfill to the
  earliest-`JoinedAt` member per household, then set non-null; add nullable `ArchivedAt`.
- `DbInitializer` seed sets `OwnerMemberId` explicitly (Du owns both seeded households).

## Web

Per the design export:

- Household-management screen (frames 1–2): household header, member list with an
  **"Ägare"** pill on the owner row, and owner-vs-member action sets ("Överför ägarskap"
  + "Arkivera hushåll" for the owner; "Lämna hushåll" for members).
- Confirmation sheets: leave (frames 3 & 6), transfer ownership (frame 4), archive
  (frame 5), restore (inline from the archived row, frame 7).
- Household switcher (frame 7): active households, then an **"Arkiverade"** section with
  a per-row **Återställ** button visible only to the owner.

## Out of scope

- **Hard delete of a non-empty household / GDPR purge / storage cleanup** — soft archival
  keeps all financial data indefinitely; only empty households can be hard-deleted
  ([ADR-0020](../adr/0020-delete-empty-households.md)). Purging households that *have*
  history remains a separate decision (ADR-0016 consequences).
- **Co-owners / multiple roles** — single owner only (rejected in ADR-0016).
- **Blocking writes to an archived household** — archived households are hidden from the
  list and read-only in practice via the UI; server-side write-blocking on archived
  households is not part of this spec.
