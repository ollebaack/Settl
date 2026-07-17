# Add-entry (unified, loan removed) addendum

Extends [implementation-map.md](implementation-map.md) §2.6 with the re-imagined
**`Ny post`** sheet in **[`Settl Add Entry.dc.html`](Settl%20Add%20Entry.dc.html)**
(new export, 2026-07-16). Supersedes the three-tab add-entry form and the `Lån` branch
described there. Same conventions: DC export is authoritative for UI structure; copy is
Swedish, shared-notebook tone.

Product decision: [unified-add-entry.md](../specs/unified-add-entry.md), evolved via the
follow-up UI pass — **`Lån` is removed as a feature**, not merely merged. "Allt"
already does exactly what a loan did (one person owes the whole amount), so the separate
IOU surface was redundant. **Every created entry is now an `expense`.** The model-side
teardown (removing the `iou` `EntryType`) is a separate, irreversible decision → see the
"Follow-ups" note; the *screen* below is settled and unaffected by how deep that goes.

---

## 1. Screens & frames

| Frame | Screen | Note |
|---|---|---|
| 1 | `Ny post` — **Utgift · Lika** | Two-tab picker; standard equal split |
| 2 | `Ny post` — **Utgift · Allt** | One person owes all; payer excluded from the picker |
| 3 | Entry detail — how "Allt" reads back | Ordinary amount-split rendering |

---

## 2. Spec

### 2.1 Type picker — three tabs → two

`Utgift / Återkommande` only. The `Lån` tab is gone
([add-entry-sheet.tsx:478](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L478)),
and with it the whole IOU branch (`Åt vilket håll` / `Med`,
[lines 511–546](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L511)).
`Återkommande` stays — it's a template (`POST /recurring`), not a ledger entry.

### 2.2 Delning order — presets first

`Lika · Allt · % · kr`. The two **one-tap presets** (`Lika`, `Allt`) sit
adjacent on the left; the **manual-entry** modes (`%`, `kr`) follow. Fixes the earlier
overflow where the label (then `Allt på en`) sat last and spilled past the row: the presets
now lead, and the label has since been shortened to `Allt`, so four equal `flex-1` pills fit
on one line at the `max-w-md` sheet width.

### 2.3 "Allt" — payer excluded

Selecting `Allt` reveals **`Vem står för hela beloppet?`** listing every member
**except the payer** (was: the full member list,
[add-entry-sheet.tsx:599](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L599)),
defaulting to the first non-payer (was: `memberList[0]`,
[add-entry-sheet.tsx:327](../../apps/web/src/components/sheets/add-entry-sheet.tsx#L327)).
This makes the balance-neutral "pays all + owes all" entry **unreachable**. In a
two-person household the picker collapses to one option (auto-selected); the summary line
`{payer} betalade · {namn} står för allt` carries it.

### 2.4 Save — always an expense

`Allt` builds `SplitInput { mode: "amount", values: { <ower>: total, <others>: 0 } }`
(ADR-0018 §2.1, unchanged) and posts `type: "expense"`. There is **no `iou` creation
path** in the UI. Detail (frame 3) renders the ordinary amount-split share rows
(`Du · betalade 0 kr`, `Sam · skyldig 1 000 kr`) — same as any amount split.

**Copy** — tabs `Utgift` / `Återkommande`; split `Lika` / `Allt` / `%` / `kr`;
payer label `Vem betalade` (reverted from the merge-era `Vem la ut?` — with loans gone
there is always a real payer); `Allt` picker `Vem står för hela beloppet?`; hint
`{namn} står för hela beloppet`; detail meta `{du/namn} betalade · {namn} står för allt`.

---

## 3. API / model surface

- **Add-entry web change** is self-contained in
  [add-entry-sheet.tsx](../../apps/web/src/components/sheets/add-entry-sheet.tsx):
  remove the `iou` tab + branch, reorder `Delning`, exclude the payer from the
  `Allt` ower picker, and drop the `type: "iou"` build path in
  `onSave()` / `buildSplit()`.
- **`POST /entries`** is now only ever called with `type: "expense"` (or `recurring`).
- **Ledger `Lån` filter** ([implementation-map.md](implementation-map.md) §2.2) is
  removed from the filter pills (`Alla / Utgifter / Repeat`).

---

## 4. Follow-ups

1. **Model teardown — decided in [ADR-0021](../adr/0021-remove-iou-entry-type.md).**
   Remove the `iou` `EntryType`, `FromMemberId`/`ToMemberId`, and the `BalanceCalculator`
   IOU branch ([BalanceCalculator.cs:54](../../apps/api/Settl.Api/Domain/BalanceCalculator.cs#L54));
   `EntryType`/`SplitMode` persist as strings, so the enum member just drops. Seed-only
   rewrite of the two `DbInitializer` `Iou(...)` calls, plus a defensive backfill
   (`UPDATE "Entries" … WHERE "Type" = 'Iou'`) in the column-dropping migration. Touches
   ~18 files + a new EF migration + `packages/api-client` regen.
2. **Regenerate `packages/api-client`** in the implementing PR if the `iou` shape leaves
   the contract (root rule).
3. **Money-math tests** — the teardown must keep `BalanceCalculator` / settle behaviour
   green; existing IOU balance tests either migrate to amount-split expectations or are
   removed with the type (api rule).
