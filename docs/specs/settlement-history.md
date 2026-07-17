# Settlement history — spec

A read-only **"Tidigare uppgörelser med {namn}"** section on the Settle-up sheet
([apps/web/src/components/sheets/settle-up-sheet.tsx](../../apps/web/src/components/sheets/settle-up-sheet.tsx),
implementation-map §2.8) that shows the settlements already recorded between the acting user
and the person the sheet is about. Today a settlement is a first-class event
([ADR-0007](../adr/0007-ledger-data-model.md)) but the only way to inspect one after the fact
is per-entry, in entry detail — there's no "what did we square, and when" view for a pair. This
surfaces that history from data we already store, so a pair can see their past clean-slates
without re-deriving anything client-side (ADR-0006).

Provenance: decided via `/grill` on 2026-07-17. No companion ADR — it reuses the existing
ledger model (ADR-0007) and adds no new decision surface; it's a read view over data we already
persist. This spec is the artifact.

## Problem

`GET /households/{id}/settle-preview` shows only the *open* net and contributing entries for a
pair. Once a settlement closes those debts they vanish from the preview (they're no longer open)
and the settlement event itself is only reachable entry-by-entry via entry detail. There is no
pairwise, chronological "these are the times we settled up" view, even though every settlement is
already a first-class `Settlement` + `SettlementClosure` record.

## Scope (v1)

- Read-only section below the current net + open entries on the Settle-up sheet. Pairwise only,
  scoped to the person the sheet is about (`?sheet=settle&person=…`).
- One row per settlement event, newest first: settled date, net amount cleared, who initiated it,
  and the count of entries that settlement closed for the pair.
- Tap a row to reveal the entries that settlement closed for the pair (title, date, amount).
- Empty state when the pair has never settled: muted "Inga tidigare uppgörelser än".
- **No** reopening/undo from history — reopening stays in entry detail as today (ADR-0007). This
  section never mutates.
- **No** new data model — reuses `Settlement` + `SettlementClosure` (ADR-0007).
- Pairwise only — no household-wide settlement log in v1.

## Data model

None. Reuses `Settlement` (who initiated, when) and `SettlementClosure` (which entry + which
debtor→creditor pair each event closed). Amounts are derived from the closed entries' frozen
shares via `BalanceCalculator.Debts`, never stored on the closure.

## API surface

`GET /households/{id}/settlements?person={memberId}` — the settlement events that closed at
least one debt in the acting-user ↔ `person` pair, newest first. Business logic in the API
(ADR-0006); money = integer minor units; timestamps UTC.

```
SettlementHistoryItemDto[] where each item =
  { id, settledAt, netClearedMinor, initiatedByMemberId, closedEntryCount, entries[] }
  entries[] = { id, title, date, amountMinor }
```

- `netClearedMinor` — signed toward the viewer across the pair debts this event closed:
  `> 0` the person owed you, `< 0` you owed the person (same convention as `settle-preview`'s
  net). Purely informational; the UI renders the magnitude.
- `amountMinor` (per entry) — the magnitude of that entry's pair debt this event closed.
- `closedEntryCount` — distinct entries this event closed for the pair (`= entries.length`).
- Settlements that closed no pair debt (e.g. a settlement that only touched a third party) are
  omitted. `entries[]` is newest-first by entry date.
- 404 for an unknown household or `person`; mirrors `settle-preview`'s guards.

New DTOs (`SettlementHistoryItemDto`, `SettlementHistoryEntryDto`) change the OpenAPI shape →
regenerate `packages/api-client` in the same PR (root rule).

## Web surface

New query hook `useSettlementHistory(householdId, person)` in
[apps/web/src/lib/queries.ts](../../apps/web/src/lib/queries.ts), keyed by household + person,
enabled only when a person is selected (mirrors `useSettlePreview`), and invalidated alongside the
rest of a household's ledger state after a settlement is recorded.

The Settle-up sheet renders the section below the open-entries list, using the existing `<Money>`
formatter and the `Card` row conventions from implementation-map §2.8 / the "Tidigare perioder"
pattern in §2.6. Section header **"Tidigare uppgörelser med {namn}"** (uppercase section label);
each row shows the settled date, the net cleared, and an initiator + count sub-line
("Du gjorde upp · N poster" / "{namn} gjorde upp · N poster"); tapping a row expands the closed
entries. Empty state: muted "Inga tidigare uppgörelser än".

## Out of scope / open questions

- Household-wide (non-pairwise) settlement history.
- Undo/reopen from the history section (stays in entry detail, ADR-0007).
- Filtering/paging the history — v1 shows the full pair history (settlements per pair are few).
