# Unified add-entry flow (merge Lån into Utgift) — spec

> **Update (2026-07-16):** the design iteration went further than "merge" — `Lån` is
> **removed as a feature** (redundant with `Allt`), so every created entry is an
> `expense`. The model-side teardown (dropping the `iou` `EntryType` + `FromMemberId`/
> `ToMemberId`) was grilled and recorded as **ADR-0020** (now the
> [Decision record](#decision-record-adr-0020) below), which amends ADR-0007 and supersedes
> the "keep both models / intent decides / `Rent lån` toggle" mechanism described in the
> sections below. The settled *UI* is in
> [add-entry-addendum.md](../design/add-entry-addendum.md); the sheet renders in
> [Settl Add Entry.dc.html](../design/Settl%20Add%20Entry.dc.html). The sections below are
> **retained for history** — read them as the superseded intermediate design, not the plan.

## Decision record (ADR-0020)

Grilled 2026-07-16 (was ADR-0020); **amends ADR-0007** and supersedes the "keep both
models" intermediate design below.

- **Remove `EntryType.Iou` and the `FromMemberId`/`ToMemberId` columns.** Every entry
  becomes an `expense`; "one owes all" is the `Allt` amount-split preset (payer share = 0,
  from ADR-0018). The `BalanceCalculator`/`NudgeCalculator` IOU branches and the `Lån`
  ledger filter are deleted.
- **`EntryType`/`SplitMode` persist as strings** (`HasConversion<string>`), so dropping the
  `Iou` enum member needs no `Type` column renumber — the column simply never holds `'Iou'`
  again.
- **Seed-only data rewrite** with a **defensive backfill** in the column-dropping migration
  (`UPDATE … WHERE "Type" = 'Iou'` + an `INSERT` of the debtor's full share) that converts
  any stray IOU to its balance-equivalent amount-split before the columns drop — a guard,
  not a full data-migration (payer = creditor, debtor share = full, `SplitMode.Amount`;
  title/category/settlement closures preserved).

The teardown is **irreversible** (a destructive column drop), safe only because no real
`iou` rows are believed to exist — the inline backfill guard is the sole protection if one
does. Directional debt with no purchase behind it (cash IOUs, opening balances) is no longer
representable; judged to have no roadmap need since settlements are first-class (ADR-0007)
and `Allt` covers one-owes-all. **Revisit** if a first-class "A owes B, no purchase" debt
becomes a requirement — that reintroduces a directional-debt model and supersedes this.

Collapse the `Ny post` sheet's three tabs (`Utgift / Lån / Återkommande`) down to
**two** — `Utgift` (which now also covers loans) and `Återkommande`. A loan (`iou`) is
no longer its own tab: it's the informal-debt *flavour* of an expense, reached through
the existing `Allt` split preset. Both entry types stay in the model
([ADR-0007](../adr/0007-ledger-data-model.md)); which one an entry becomes is decided by
**intent, not by the split math** (the balance math is already identical). Extends the
add-entry screen in [implementation-map.md](../design/implementation-map.md) §2.6 and the
`Allt` preset from [ADR-0018](ledger-editing.md) §2.1. The
merged form's layout is designed in a follow-up `ui-design` pass; this spec fixes its
shape and behaviour.

Provenance: decided via `/grill` on 2026-07-16. Spec only, no companion ADR — the grill
chose to record the model-adjacent call (deprecate the `Lån` tab, keep both entry types)
here rather than as a standalone decision. It rests on existing ADRs 0007 (entry model)
and 0018 (`Allt`), which it extends rather than reverses; nothing here needs a
migration or a schema change, so by `docs/README.md`'s bar it is not ADR-worthy.

## Problem

There are **two UI paths to record the exact same debt**, and the newer one shipped only
days ago:

1. The **`Lån` tab** — directional `fromMemberId → toMemberId`, full amount, one debt.
2. **`Utgift` → `Allt`** — an expense with a zero payer share; ADR-0018 §2.1 itself
   flags this as "intentional overlap [that] must not double-count."

Downstream they are already **one thing**: `BalanceCalculator` collapses both to `Debt`
records ([BalanceCalculator.cs:54](../../apps/api/Settl.Api/Domain/BalanceCalculator.cs#L54)).
The duplication surfaces as UX confusion — including a reachable **no-op entry**: in
`Allt` the "who owes the whole amount" picker renders the full member list
([add-entry-sheet.tsx:599](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L599))
and defaults to the first member regardless of who paid
([add-entry-sheet.tsx:327](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L327)),
so "Julia paid + Julia owes all" (a balance-neutral entry) is not just possible, it's the
default.

## Scope (v1)

- **`Ny post` drops to two tabs:** `Utgift` and `Återkommande`. The `Lån` tab is removed
  from the type picker ([add-entry-sheet.tsx:478](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L478)).
  `Återkommande` stays separate — it's a *template* (`POST /recurring`), not a ledger entry.
- **The loan case moves inside `Allt`.** The `Delning` editor
  (`Lika / % / kr / Allt`) becomes the single home for "who owes what." Choosing
  `Allt` reveals who bears the whole amount plus the direction sense that the old
  `Lån` branch carried (`Jag är skyldig` / `Skyldig mig`, `Med`).
- **Emit rule — intent decides the type, not the split shape:**
  - `Lika / % / kr` → **`expense`** (unchanged).
  - `Allt` for a **real purchase someone paid for** → **`expense`** with a zero
    payer share (ADR-0018 behaviour preserved; reads back `Du betalade · {namn} står för
    allt`, keeps category).
  - `Allt` marked as a **bare debt with no purchase** ("jag lånade ut 500", "jag är
    skyldig dig 200") → **`iou`**, with `from`/`to` derived from payer + who-owes. Renders
    as today's `Lån till/från {namn}`, `Informellt lån — inget kvitto behövs`.
- **Fixes the no-op bug as a side effect:** in `Allt`, the "who owes the whole
  amount" picker **excludes the payer** and defaults to the first non-payer, so
  payer == ower is unreachable. In a two-person household this auto-resolves to the only
  valid choice.
- **No double-count:** an `Allt` entry is *either* an expense *or* an iou, never
  both — the existing ADR-0018 §2.1 invariant, now structural rather than a caution.
- **No API, DTO, or schema change; no migration.** `POST /entries` already accepts both
  `type: "expense"` and `type: "iou"`; the merge is entirely a web-form change. Existing
  `iou` entries render unchanged and `Lån` stays a ledger filter
  ([implementation-map.md](../design/implementation-map.md) §2.2).

## Web surface

- **[add-entry-sheet.tsx](../../apps/web/src/components/sheets/add-entry-sheet.tsx)** is
  the only file that changes:
  - Remove the `Lån` tab; the `iou` branch (`Åt vilket håll` / `Med`,
    [lines 511–546](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L511)) moves
    into the `Allt` sub-panel
    ([lines 590–611](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L590)).
  - `buildSplit()` / `onSave()` gain the intent branch: `Allt` + "bare loan"
    signal → build the `iou` `CreateEntryRequest` (`fromMemberId`/`toMemberId` derived
    from payer + ower) instead of an `amount` split; everything else unchanged.
  - The `Allt` ower picker filters out the payer and defaults to the first
    non-payer.
- Entry detail, ledger filters, and `BalanceCalculator` are untouched — both types
  already render and net correctly.
- Built from a `ui-design` pass; the visual layout (how `Allt` presents payer,
  ower, direction, and the loan-vs-purchase signal in one coherent panel) is produced
  there and mirrored into `docs/design/`.

## Out of scope / open questions

- **The loan-vs-purchase signal inside `Allt`** — the one micro-UX detail the
  design pass must nail: how the user marks "this is a bare loan, no purchase" (leading
  candidate: a small `Rent lån — inget köp` toggle that hides title/category and stores
  the `iou`; direction *derived* from payer + ower rather than a separate toggle). The
  balance is identical either way, so the choice is purely about framing and the
  metadata (glyph, category, `Informellt lån` caption).
- **Model collapse** (removing the `iou` `EntryType` and `from`/`to`, migrating rows to
  amount-split expenses) — explicitly rejected in the grill: a migration reversing
  ADR-0007/0018 for a cosmetic win, when `BalanceCalculator` already unifies them.
- **Recurring loans** — out of scope; `Återkommande` stays expense/split-shaped.

## Rejected alternatives (from the grill)

- **Collapse the model too** — remove `iou`, migrate existing rows. Reverses a
  just-shipped decision for no balance-math gain.
- **Always emit `iou` for `Allt`** (delete the ADR-0018 expense sugar) — a paid
  purchase where one person owes everything would lose its `Du betalade` + category
  framing and read as `Lån`.
- **Always emit `expense`** (direction toggle merely presets payer + `Allt`) —
  leaves the `iou` type dead and vestigial, contradicting "keep the model."
- **A top-level `Delad utgift / Lån` segmented control** — clearer for a pure loan but
  keeps two forms in a trenchcoat; the merge into the `Delning` editor is more genuinely
  unified and also fixes the no-op bug.
