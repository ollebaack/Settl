# ADR-0021: Remove the IOU entry type (loans are amount-split expenses)

- **Status:** accepted
- **Date:** 2026-07-16

## Context

The add-entry UI carried three entry types (`Utgift / Lån / Återkommande`), but the
`Lån` (`iou`) path turned out to be redundant: the `Allt på en` split preset added in
[ADR-0018](0018-ledger-editing-affordances.md) already expresses "one person owes the
whole amount," and `BalanceCalculator` collapses both to the same `Debt` records. A
`/grill` first kept both models behind one form ([unified-add-entry.md](../specs/unified-add-entry.md));
a follow-up UI pass then decided `Lån` should be removed outright, and a second grill
(2026-07-16) confirmed tearing the type out of the model rather than leaving it dormant.
Only demo seed data uses `iou` today.

## Decision

- **We will remove `EntryType.Iou` and the `FromMemberId`/`ToMemberId` columns.** Every
  entry becomes an `expense`; "one owes all" is the `Allt på en` amount-split preset
  (payer share = 0). The `BalanceCalculator`/`NudgeCalculator` IOU branches and the `Lån`
  ledger filter are deleted.
- **`EntryType`/`SplitMode` persist as strings** (`HasConversion<string>`), so dropping the
  `Iou` enum member needs no `Type` column change and can't renumber `RecurringPost` — the
  `Type` column simply never holds `'Iou'` again.
- **Seed-only data rewrite:** the two `DbInitializer` `Iou(...)` calls become `Expense` +
  `Allt på en`. The column-dropping EF migration carries a **defensive backfill**
  (`UPDATE "Entries" … WHERE "Type" = 'Iou'`, plus an `INSERT` of the debtor's full share)
  that converts any stray IOU to its amount-split equivalent *before* the columns drop — a
  guard, not a full data-migration. The mapping
  is balance-equivalent: payer = creditor (`to`), shares debtor (`from`) = full /
  creditor (`to`) = 0, `SplitMode.Amount`; title and category preserved; settlement
  closures (keyed by entry id + unordered pair) untouched.

## Consequences

This **reverses the prior unified-add-entry grill's "keep both models"** and **amends
ADR-0007**, which listed `iou` as a first-class entry type — that clause no longer holds.
Directional debt with no purchase behind it (cash IOUs, manual balance adjustments,
opening balances) is no longer representable; we judged there is no roadmap need, since
settlements are already first-class events (ADR-0007) and `Allt på en` covers one-owes-all.

The teardown is **irreversible** (a destructive column drop). The seed-only choice is safe
only because no real `iou` rows are believed to exist; the inline backfill guard is the
sole protection if one does — without it a surviving `Type=1` row would fall through to the
shares path, find a null payer, and **silently lose its debt** from every balance. IOU
balance/endpoint tests get rewritten to amount-split expectations or removed; the money-math
suite must stay green (api rule); `packages/api-client` is regenerated in the implementing PR.

**Revisit** if a first-class "A owes B, no purchase" debt becomes a requirement — that would
reintroduce a directional-debt model (new columns or a distinct entity) and supersede this
decision.
