# Ledger-editing addendum

Extends `implementation-map.md` §2.6–2.9 with the screens in
**`Settl Ledger Editing.dc.html`** (new export, 2026-07-16). Covers product
wishlist items 6–8, grilled into **ADR-0018**
(`docs/specs/ledger-editing.md`). Same conventions: UI structure
from the DC export is authoritative; copy is Swedish, shared-notebook tone.

**Unlike the auth addendum, the API mostly already exists** — edit (`PUT /entries/{id}`),
delete (`DELETE /entries/{id}`, hard), recurring pause (`PATCH /recurring/{id}`) are
built. The only new endpoint is recurring template delete. This is a **missing/safer
UI** over existing behavior, not a new contract.

---

## 1. Screens & frames

| Frame | Screen | Extends |
|---|---|---|
| 1 | Add entry — `Delning` gains an **"Allt"** preset | §2.6 |
| 2 | Entry detail — how the preset reads back | §2.7 |
| 3 | Entry detail — **⋯ action menu** (Redigera / Ta bort) | §2.7 |
| 4 | **Redigera post** — the add-entry form, prefilled | §2.6 / §2.7 |
| 5 | Entry detail — **locked by settlement** (reopen first) | §2.7 |
| 6 | **Ta bort posten?** — confirm dialog | §2.7 |
| 7 | **Ångra-toast** — ~5 s deferred delete | §3 (toasts) |
| 8 | Recurring detail — ⋯ menu, **clean template → Ta bort** | §2.9 |
| 9 | Recurring detail — **has history → pause only** | §2.9 |
| 10 | **Ta bort {mall}?** — clean-delete confirm | §2.9 |

---

## 2. Per-feature spec

### 2.1 "Allt" split preset (frames 1–2) — wishlist #6

A fourth `Delning` mode alongside `Lika / % / kr`. Selecting it reveals a
single-select member list (`Vem står för hela beloppet?`); the chosen member's
share = the full amount, everyone else = 0. The payer picker (`Vem betalade`) is
**unchanged** — its default stays. This is the fix for the "Du betalade"
confusion: the gap was split flexibility, not the payer model (ADR-0018).

- **No model change, no new `SplitMode`.** The preset is **syntactic sugar over
  `amount` mode**: it builds `SplitInput { mode: "amount", values: { <chosen>: total,
  <others>: 0 } }`. The API freezes it exactly like any amount split
  (`ShareFreezer.Freeze`, ADR-0007). Detail (frame 2) therefore renders the ordinary
  amount-split share rows (`Du · betalade 0 kr`, `Sam · skyldig 1 000 kr`).
- **Overlap with Lån (IOU) is intentional and must not double-count.** "I paid, Sam
  owes it all" is one expense with a zero payer share — a single ledger entry, one
  set of frozen shares. It is *not* also an IOU; do not emit both.
- Client validation reuses the existing amount tolerance (±0.05 kr); the preset always
  balances by construction, so the live hint reads `{namn} står för hela beloppet`.

**Copy** — mode label `Allt`; picker label `Vem står för hela beloppet?`;
hint `{namn} står för hela beloppet`; detail meta `Du betalade · {namn} står för allt`.

### 2.2 Edit affordance (frames 3–5) — wishlist #8

- **Entry point:** a `⋯` icon button (top-right of the entry-detail sheet, chip bg)
  opens a `DropdownMenu`: **Redigera** → the add-entry form in edit mode; **Ta bort**
  (destructive) → §2.3.
- **Edit reuses the add-entry sheet** prefilled from the entry (`GET /entries/{id}`),
  title `Redigera post`, save button `Spara ändringar`, secondary `Avbryt`. Save →
  `PUT /entries/{id}` (rebuild-in-place, existing). Success toast `Ändrad`.
- **Locked state (frame 5):** when a `SettlementClosure` references the entry the API
  returns **409**. Surface it as a disabled menu (both items greyed) + caption
  `Låst — öppna igen för att ändra eller ta bort.`, plus an inline warnbox
  `Posten är låst`. The **reopen path already exists**: the settle toggle
  (`Reglerad ✓ — tryck för att öppna igen` → `DELETE /entries/{id}/settlements`).
  After reopen, edit/delete unlock.
- **Edit itself gets no undo** — a mis-edit is recovered by editing again (ADR-0018).

**Copy** — `Redigera`; `Redigera post`; `Spara ändringar`; `Avbryt`; `Ändrad` (toast);
locked menu caption `Låst — öppna igen för att ändra eller ta bort.`; warnbox
`Posten är låst` / `En uppgörelse rör den här posten. Öppna igen innan du ändrar eller
tar bort — då rörs bara den här posten.`

### 2.3 Delete affordance (frames 6–7) — wishlist #7

- **Hard delete stays** (ADR-0007/0018). `Ta bort` → confirmation dialog
  `Ta bort posten?` with a warnbox `Det här ändrar saldot för alla som var med` and
  the entry title + amount.
- **Undo toast:** on confirm the entry is optimistically removed from the list and a
  Sonner toast `Posten togs bort` + **`Ångra`** action shows for **~5 s**; the actual
  `DELETE /entries/{id}` **fires only after the window elapses**. `Ångra` cancels the
  pending delete (nothing hit the server). Undo is **delete-only**.
- **Best-effort, per-device** — navigating away/closing the tab abandons the pending
  delete (entry resurrects on reload); a delete is invisible to other members. Accepted
  shortcut, **tech-debt 0010**.
- **Locked (409):** same lock story as §2.2 — a settled entry can't be deleted; reopen
  first.

**Copy** — dialog `Ta bort posten?`; warnbox `Det här ändrar saldot för alla som var
med` / `”{titel}” · {belopp} tas bort ur loggboken. Du kan ångra direkt efteråt.`;
caption `Borttagningen görs efter några sekunder — hinner du ångra händer inget.`;
buttons `Ta bort` / `Avbryt`; toast `Posten togs bort` / `Ångra`; post-window toast
(optional) `Borttagen`.

### 2.4 Recurring deletion (frames 8–10) — wishlist #8

- **Delete-if-clean, else deactivate.** Recurring detail gains the same `⋯` menu:
  **Redigera** + **Ta bort**.
  - **Clean template** (has posted zero entries, frame 8): `Ta bort` enabled → confirm
    dialog `Ta bort ”{titel}”?` offering `Ta bort` / **`Pausa i stället`**.
  - **Has posted history** (frame 9): `Ta bort` is **disabled** with caption
    `Har bokförda perioder — pausa i stället, så finns historiken kvar.` Only
    `Pausa autobokföring` (existing `PATCH { active:false }`) is offered.
- **New endpoint:** `DELETE /recurring/{id}` — 204 when the template has no posted
  entries; **409** (`Den återkommande posten har bokförda perioder`) when
  `Entries.Any(e => e.RecurringTemplateId == id)`. Mirror the entries lock pattern.
- **Posted recurring entries are ordinary entries** — deleted individually in the
  ledger via §2.3 (normal delete + undo), not through the template.

**Copy** — clean note `Ingen period bokförd än — inget i loggboken påverkas om du tar
bort den.`; disabled caption `Har bokförda perioder — pausa i stället, så finns
historiken kvar.`; history hint `De bokförda posterna är vanliga poster — ta bort dem
var för sig i loggboken.`; dialog `Ta bort ”{titel}”?` / `Den har inte bokfört någon
post än, så inget försvinner ur loggboken. Vill du behålla den för senare kan du pausa
i stället.`; buttons `Ta bort` / `Pausa i stället`; toast `{titel} borttagen`.

---

## 3. API contract summary

| Endpoint | Status | Note |
|---|---|---|
| `PUT /entries/{id}` | **exists** | Edit / rebuild-in-place; 409 when locked. |
| `DELETE /entries/{id}` | **exists** | Hard delete; 409 when locked. Undo is client-side (defer the call). |
| `DELETE /entries/{id}/settlements` | **exists** | Reopen path for locked entries. |
| `GET /entries/{id}` | **exists** | Prefill the edit form. |
| `PATCH /recurring/{id}` `{ active }` | **exists** | Deactivate (pause) — the fallback for templates with history. |
| `DELETE /recurring/{id}` | **NEW** | 204 if no posted entries; 409 otherwise. Regenerate `packages/api-client` in the PR that adds it (root rule). |

---

## 4. Notes & follow-ups

1. **No schema change.** No `CreatedByMemberId`, no nullable payer, no soft-delete —
   all rejected in ADR-0018. The "Allt" preset stores an ordinary amount split.
2. **`DELETE /recurring/{id}` is the only API-shape change** — new operation +
   regenerated client + a `WebApplicationFactory` test (api rule), incl. the 409-with-
   history case.
3. **Undo durability is deliberately weak** (tech-debt 0010) — if it ever needs to be
   cross-user or survive navigation, that's a soft-delete decision → re-grill.
4. **Detail meta for the preset** currently reuses the amount-split rendering. A
   friendlier `{namn} står för allt` label (frame 2) needs the API to distinguish "one
   person owes all" from a general amount split — deferred; not worth a flag on the
   entity for v1.
5. **Menu component:** add shadcn `dropdown-menu` usage to entry-detail + recurring-detail
   (already installed for the desktop household switcher, §5); `alert-dialog` for the
   confirm dialogs (new install).
