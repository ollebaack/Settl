# Settlement history — spec

Show people what they've already settled. Settlements are first-class events in the ledger
(ADR-0007) but the app never surfaces them after the fact — there's no "we cleared 934 kr on
the 12th" record. This adds a read-only per-person history to the Settle-up sheet, turning
existing data into a trust-building surface. Built on the existing `Settlement` +
`SettlementClosure` model; the Settle-up UI is `apps/web/src/components/sheets/settle-up-sheet.tsx`.

Provenance: decided via `/grill` on 2026-07-17. Not ADR-worthy — no hard-to-reverse or
architectural decision, just the shape of a read model over data that already exists.

## Problem

When two people settle, `POST /households/{id}/settlements` records one `Settlement` event
with a `SettlementClosure` per closed pair-debt, and affected entries flip to `reglerad`
(derived). But the event itself is then invisible: the Settle-up sheet only shows the
*current* net and open contributing entries, and entry detail shows a single entry's settled
state. Nobody can answer "when did we last square up, and for how much?" — the moment that
gives a shared-ledger app its trust payoff is thrown away.

## Scope (v1)

- **Placement:** a **"Tidigare uppgörelser med {namn}"** section on the Settle-up sheet, below
  the current net + open entries. Pairwise only — scoped to the person the sheet is about.
- **One row per settlement event**, newest first: settled date, net amount cleared in that
  event, who initiated it, and a count of entries closed. Tapping a row reveals the entries
  that settlement closed (title, date, amount).
- **Read-only.** Reopening stays exactly where it is today — in entry detail (delete the
  settlement per ADR-0007). No undo-from-history in v1.
- **No** whole-household settlement log, no export, no filtering — pairwise list only.

## Data model

None. Reuses `Settlement` and `SettlementClosure` as-is (ADR-0007). No new tables, columns,
or migration.

## API surface

- New read endpoint: **`GET /households/{id}/settlements?person={memberId}`** →
  the settlement events touching the acting user ↔ `person` pair, each as
  `{ id, settledAt, netClearedMinor, initiatedByMemberId, closedEntryCount, entries[] }`
  where `entries[]` is `{ id, title, date, amountMinor }` for the tap-to-expand detail.
  Money in integer minor units, timestamps UTC (project rules); amounts server-derived from
  the closures (ADR-0006 — the SPA renders, never computes). Add a `WebApplicationFactory`
  integration test (API rule). Regenerate `packages/api-client` in the same PR (root rule).

## Web surface

- Extend `apps/web/src/components/sheets/settle-up-sheet.tsx` with the history section and a
  new query hook in `apps/web/src/lib/queries.ts` (keyed by household + person, enabled when
  a person is selected). Rows use the existing `<Money>` formatter and row conventions from
  the design (`docs/design/implementation-map.md` §2.8). Empty state: a muted "Inga tidigare
  uppgörelser än" when the pair has never settled. Confirm final Swedish wording with design.

## Out of scope / open questions

- **Whole-household history view** (a book-level log of every settlement) — deferred; revisit
  if a per-person list proves too narrow.
- **Undo a whole settlement event from history** — deliberately excluded; reopening one event
  un-settles every entry it closed, which needs a loud confirm and an ADR-0007 locking review.
  Consider as a separate grill if requested.
- Exact copy for the section header, initiator phrasing ("Du gjorde upp" vs "{namn} gjorde
  upp"), and the empty state — design to confirm.
