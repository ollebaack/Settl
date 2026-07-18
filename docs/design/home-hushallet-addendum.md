# Home + Hushållet addendum

Extends `implementation-map.md` and the [multi-household overview addendum](multi-household-overview-addendum.md)
with the screens in **`Settl Home + Hushallet.dc.html`** (new export, 2026-07-17) and
realizes **[ADR-0021](../specs/adaptive-home-multi-household-overview.md#decision-record-adr-0021)**. Same conventions: UI
structure from the DC export is authoritative; API fields below are **proposed
contracts**. Business logic (net, netLabel, shares, settled state) is server-derived —
the screens render and call, never compute (ADR-0006).

**Core IA change (ADR-0021, supersedes the adaptive-home behavior in
[adaptive-home-multi-household-overview.md](../specs/adaptive-home-multi-household-overview.md)):**
`/` is now **always the Overview** — for one household too, not adaptively. A single new
tab, **Hushållet**, replaces the Loggbok tab and merges the per-household dashboard and
the full ledger into **one scroll** for the active household. The nav stays four tabs:
**Hem · Hushållet · På repeat · Aktivitet**.

---

## 1. Route table (changes)

| Route | Screen (sv / en) | Behaviour | Change from today |
|---|---|---|---|
| `/` | **Hem** / Home | **Always the Overview** (multi-household portfolio), regardless of household count. Frames 1–2. | Was adaptive (1 hh → dashboard). The single-household collapse in the overview addendum §2.3 is **removed**. |
| `/ledger` (repurposed) or `/boken` | **Hushållet** / The book | Merged dashboard + ledger for the **active** household, one scroll (frame 3). | Replaces the standalone Loggbok tab. `/ledger` as a bare log **goes away** — deep links to it redirect here. |
| `/hushall/$id` | **Hushållet** (drill-in) | Same merged component as the tab, for a book entered from the Overview. Shows the `‹ Översikt` back link. | Reuses the frame-3 component — one view, two entry points (ADR-0021). |

**Active-household resolution (ADR-0021):** tapping a book card on the Overview sets it
active and navigates to Hushållet; the tab always shows the active book. Single-household
users have exactly one active book, so the tab just works. Whether Hushållet is a state
of a stable route (`/boken`) or the repurposed `/ledger` is an implementation choice for
the PR — the design fixes the screen, not the URL.

The **`+ Ny post`** FAB and the four sheets (`add`, `entry`, `settle`, `recurring`,
`households`) are unchanged and open over whatever screen is active.

---

## 2. Per-screen spec

### 2.1 Hem — Overview (frames 1–2)

Unchanged from the [multi-household overview addendum](multi-household-overview-addendum.md)
§2.1–2.2 (roll-up hero, one card per book, `Vänner`, `Arkiverade`, single- vs
mixed-currency hero), **except**:

- **No single-household collapse.** With exactly 1 active household the Overview still
  renders: roll-up hero scoped to that book (`Du ska få totalt` + sub `i {namn} · {m}
  öppna poster`) and a single book card. The card's right side is an `Öppna` affordance
  (frame 2) instead of a net, since the net already fills the hero. Helper copy: *"Med
  bara ett hushåll är Hem ändå översikten — hoppa in i boken via Hushållet."*
- Book cards and the bottom tab bar are the same at every count.

**Data** — `GET /households` → `HouseholdListItemDto[]` (`id, name, currency,
memberNames, netMinor, netLabel`) as before. The single-household hero reuses that one
row's `netMinor`/`netLabel`/`openCount`; no new endpoint.

### 2.2 Hushållet — merged book (frame 3)

The working surface for one household, top to bottom in a single scroll:

1. **`‹ Översikt` back link** — present when entered from the Overview (`/hushall/$id`)
   and, for multi-household users, on the tab too. Returns to `/`.
2. **Header** — household bubble (rounded square) + name + `memberNames`; overlapping
   member avatars (circles) on the right. Keeps the bubble-hierarchy rule (household =
   rounded square, person = circle).
3. **Net hero** — identical to today's home hero: uppercase `netLabel`, 36px mono net
   (color by sign), sub `{openCount} öppna poster i det här hushållet`.
4. **Saldon** — per-person balance rows, **each with a prominent `Gör upp` chip**
   (accent-filled pill, right-aligned) opening the settle sheet for that person. Row =
   avatar (circle) + name + signed mono net with a `· skyldig dig` / `· du är skyldig`
   gloss. A square person shows a muted `Kvitt` pill and no chip (`opacity .6`). This is
   the ADR-0021 "prominent per-person settle" — the whole row still opens the sheet, but
   the chip makes the action visible without a tap-to-discover.
5. **På gång** — the mobile upcoming rail (dashed ghost cards, horizontal scroll),
   active-household-scoped. On desktop this stays in the right rail (see §4).
6. **Loggbok** — section header `Loggbok` + open-count; the **filter pills** (`Alla /
   Utgifter / Lån / Repeat`); then the full chronological `<EntryRow>` list grouped by
   day, exactly as the standalone Loggbok renders today. Same glyph tiles (Lucide
   category icons; IOU `⇄`, recurring `↻` on soft/accent), meta line, and right-column
   mono amount + derived sub-status from `implementation-map.md` §2.

**Data (proposed API)** — no new endpoints; this composes what Hem and Loggbok already
call, scoped to the active household:
- Hero + `Saldon` + `upcoming`: `GET /households/{id}/summary` (`overallNet`, `netLabel`,
  `openCount`, `people[]` incl. `relation`, `upcoming[]`) — the existing home summary.
- Log: `GET /households/{id}/entries?filter={all|expense|iou|recurring}&sort=date_desc`,
  grouped by day client-side. Pagination/infinite-scroll as the standalone Loggbok.
- `Gör upp` chip → the existing settle sheet (`?sheet=settle&person=…`) and its
  settlement endpoint. **Per-person only — no aggregate "settle all" (ADR-0021).**

### 2.3 Gör upp (frame 4)

The per-person settle sheet, **unchanged** — shown only to document that the `Gör upp`
chip opens it (net with one person, contributing entries, `Markera allt som reglerat`).
Specified in `implementation-map.md`; not re-specified here.

---

## 3. Copy glossary (sv → en, additions)

| sv | en |
|---|---|
| Hushållet | The book (this household) |
| ‹ Översikt | ‹ Overview |
| Saldon | Balances |
| Gör upp | Settle up |
| Kvitt | Settled |
| skyldig dig / du är skyldig | owes you / you owe |
| Öppna | Open |
| {n} öppna poster i det här hushållet | {n} open items in this household |
| Ditt hushåll | Your household |
| i {namn} · {m} öppna poster | in {name} · {m} open items |
| Med ADR-0021 är Hem alltid översikten — även med ett hushåll. | With ADR-0021, Home is always the overview — even with one household. |

---

## 4. Desktop adaptation (not drawn — mobile-first export)

On ≥980px, `<AppShell>` keeps its 3-column grid. **Hem** replaces the centre column with
the Overview (book cards may lay out 2-up). **Hushållet** puts the merged content in the
centre column with `Saldon` + net hero at the top and the day-grouped log below;
`På gång` and `Knuffar` stay in the right rail (active-household-scoped), so the centre
column drops its inline `På gång` rail on desktop — as the standalone Home does today.
The sidebar's nav swaps the Loggbok item for **Hushållet**. Flag for the PR.

---

## 5. Notes & follow-ups

1. **`/ledger` redirect.** Repurposing the route means old `/ledger` deep links (and the
   dashboard's former *Visa alla* link) must redirect to Hushållet. Add the redirect in
   the implementing PR.
2. **Supersedes the adaptive-home spec.** `adaptive-home-multi-household-overview.md`'s
   adaptive `/` and single-household collapse (§2.3 of its addendum) are replaced by
   ADR-0021. The overview frames themselves (currency roll-up, archived, friends) are
   unchanged and still governed by that spec/addendum.
3. **No API-shape change expected.** This is an IA/frontend reshuffle over existing
   proposed contracts (`/households`, `/households/{id}/summary`, `/entries`, settlement).
   If a build introduces a new field (e.g. a stable "active household" pointer), regenerate
   `packages/api-client` in that PR (root rule).
4. **Per-person settle is load-bearing (ADR-0021).** The chip must never become an
   aggregate "settle everything" — that is explicitly rejected and would need new
   settlement semantics.
