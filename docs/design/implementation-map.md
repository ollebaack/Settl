# Settl implementation map

Synthesis of the design exports in `docs/design/` into one build spec for `apps/web`.

**Source precedence (binding):**

- **Model** disagreements → **ADR-0007** wins (`docs/adr/0007-ledger-data-model.md`).
- **UI structure / interactions** → **`Settl Prototype.dc.html`** is authoritative (original English prototype; behavior canonical, copy/currency NOT). Note: the prototype's **IOU / `Lån` entry type was removed** (ADR-0020) — "one owes all" is now the `Allt` split preset, not a separate type; ignore the prototype's IOU affordances.
- **UI copy / Swedish wording / SEK / formatting** → **`Settl App`** wins (the canonical Swedish app, embedded and rendered by `Settl Web.dc.html`).
- `Settl Web.dc.html` is a **responsive harness** (renders `Settl App` in a desktop + mobile frame). It is NOT a screen — build the app once, responsively. `support.js` is the generic dc-runtime and contains zero app logic.
- `Settl Shadcn Handoff.dc.html` gives the theme tokens, component mapping, and behavioral must-haves.
- The `test` inventory is empty and contributes nothing.
- **Screens shipped after this synthesis** are each specified in their own ADR + design **addendum** (see `docs/design/*-addendum.md` and `docs/adr/`). Where an addendum owns a screen, this map keeps its entry thin and defers to the addendum rather than re-specifying it; the route table and cross-references here are kept accurate.

**Stack:** React 19 + Vite, TanStack Router (file-based routes in `src/routes/`) + TanStack Query, Tailwind v4 semantic tokens only, shadcn (Base UI / Luma). Business logic lives in the API (ADR-0006) — the SPA renders and calls, never computes balances, shares, nudges, or settled state.

**Current repo state:** the app **and** the API are built. `src/routes/` holds the full route set (Overview, Hushållet, På repeat, Statistik, Profil, Kontakter, household management, and the auth flow — see §1), `src/components/ui/` holds the full installed shadcn set (§5), and `src/components/sheets/` holds every overlay. The API implements the ADR-0007 contract — see `docs/design/api-contract.md` for the **binding server shape** and `packages/api-client` for the generated TS types. The `Data —` blocks below describe the **actual contract** (kept in sync with `api-contract.md`, not proposals); any API-shape change still regenerates `packages/api-client` in the same PR (root rule).

---

## 1. Route table

The core screens are TanStack file-based routes. The overlays are the **same bottom-sheet pattern** (Drawer on mobile / Dialog on desktop) driven by a `sheet` **search param** on whatever route is active, so they are deep-linkable and the underlying screen stays mounted. Only one overlay open at a time; the full set is mapped in `src/components/sheet-router.tsx`. Screens shipped after this synthesis defer to their addenda (see the **Defined by** column); the per-screen §2 entries for them are kept thin.

**Core screens (file-based routes):**

| Route | Screen (sv / en) | Purpose | Defined by |
|---|---|---|---|
| `/` | **Hem** / Home | Multi-household Overview: roll-up hero (single- or mixed-currency), per-book cards, `Vänner` + `Arkiverade`, add-number banner (ADR-0026). Always the Overview, even for one book. | ADR-0021 + multi-household-overview-addendum + home-hushallet-addendum (supersedes the single-book "Home", §2.1) |
| `/hushallet` | **Hushållet** / The book | Merged dashboard + ledger for the **active** household, one scroll: net hero → `Saldon` (per-person, `Gör upp` chip) → `På gång` → `Loggbok` (search, filter pills, day-grouped rows) | **ADR-0021** + home-hushallet-addendum (§2.2, §2.11) |
| `/hushall/$id` | **Hushållet** (drill-in) | Same `<HouseholdBook>` component, for a book entered from the Overview; sets it active | ADR-0021 (one view, two entry points) |
| `/ledger` | — (retired) | **Redirects to `/hushallet`.** The standalone Loggbok tab is gone (ADR-0021); deep links redirect. | ADR-0021 |
| `/recurring` | **På repeat** / Recurring | Recurring templates, monthly totals, cycle progress, pause/resume, end-date (`Avslutad`) | Settl App; Prototype "Repeats"; recurring-end-date spec |
| `/statistik` | **Statistik** / Statistics | Per-person monthly contribution chart (who paid what, trailing 12 months). Takes the 4th tab slot. | household-statistics spec |
| `/activity` | **Notiser** / Activity | Event-driven nudges (3 triggers) + triggers reference card. **No tab slot** — reached via the mobile header bell and the desktop right rail (§3). | Settl App; ADR-0023; reminder-delivery + ADR-0024 |
| `/profil` | **Profil** / Profile | Name, avatar emoji, the member's **single number** ("Ditt nummer" — powers Swish, ADR-0026), nudge-email opt-in switch (off by default), sign out | profile-addendum + **ADR-0026** + reminder-delivery |
| `/kontakter` | **Kontakter** / Contacts | Saved contacts + pending invites; blind add-by-number/email invite; **no own-number input** (moved to Profil, ADR-0026) | ADR-0019 + contacts-addendum |
| `/household` | **Hushåll** / Manage household | Members roster, owner (`Ägare`) actions: transfer / archive / delete; member: leave | ADR-0016 + ADR-0022 + household-ownership spec |
| `/login`, `/signup`, `/verify-email`, `/confirm-email`, `/reset-password`, `/accept-invite` | Auth & onboarding | Sign in / up, email verification, password reset, invite accept — no `<AppShell>` chrome | ADR-0005 + ADR-0011 + auth-onboarding-addendum |

**Overlays (`?sheet=…` search params, mapped in `sheet-router.tsx`):**

| Sheet param | Screen (sv / en) | Purpose |
|---|---|---|
| `?sheet=households` | **Dina hushåll** / Household switcher | Switch household (many-to-many); archived section; invite section |
| `?sheet=newHousehold` | **Nytt hushåll** / New household | Create a book (name only; invite others afterward) |
| `?sheet=add` / `?sheet=edit&id=…` / `?sheet=editRecurring&id=…` | **Ny post** / **Redigera post** / Add or edit entry | Create/edit expense or recurring (no IOU type, ADR-0020) |
| `?sheet=entry&id=…` | Entry detail | Inspect entry, per-person shares, category, settle/reopen, edit/delete |
| `?sheet=settle&person=…` | **Gör upp med {namn}** / Settle-up | Net with one person, contributing entries, Swish pay, settle all, history |
| `?sheet=recurring&id=…` | Recurring detail | Next post, split, past cycles, end-date, pause/resume |
| `?sheet=addFriend` | **Lägg till en vän** / Add friend | Invite-by-number/email affordance (Overview) |
| `?sheet=leaveHousehold` / `transferOwnership` / `archiveHousehold` / `deleteHousehold` | Household-management sheets | Confirm flows for `/household` (ADR-0016, ADR-0022) |
| `?sheet=avatar` (on `/profil`) | **Välj profilbild** / Choose avatar | Emoji picker + "use my letter" reset (profile-addendum) |

**Responsive shell (not a route):** `wide = innerWidth >= 980`. Desktop = 3-column grid `220px minmax(0,1fr) 300px`, max-width 1240px, center feed capped 640px. Mobile = single column, root `max-w-md`, sticky header + bottom tab bar (`padding-bottom` clears the bar). Overlays: centered **Dialog** on desktop, bottom **Drawer** (vaul) on mobile — one responsive wrapper component (`ResponsiveSheet`).

---

## 2. Per-screen spec

Shared row conventions used by the Hushållet `Loggbok` list (build once as `<EntryRow>`, `src/components/entry-row.tsx`). Only two entry types exist — `expense` and `recurringPost` (ADR-0020 removed IOU):

- **Glyph tile** (36px rounded): recurring post `↻` (U+21BB, soft/accent bg); every other (expense) row shows its **category icon** — a Lucide icon selected from the entry's stored `Category` (ADR-0012; category assigned server-side from the title, 12 groups + `Other`), on chip bg. (The old first-letter glyph and the IOU `⇄` glyph are gone.)
- **Meta line:** expense → `Du betalade` / `{namn} betalade`; recurring post → `Bokförd automatiskt · {du betalar | {namn} betalar} · {datum}` (English gloss: "you paid / name paid"; "posted automatically · … · date").
- **Right column:** mono amount + colored sub-status keyed by `ViewerStatusKind` (all **derived from API**, never a stored flag): `settled` → `reglerad` at `opacity .5`; `youOwe` → `du är skyldig {belopp}` (destructive); `youAreOwed` → `du ska få {belopp}` (`--success`); `partiallySettled` → `din del reglerad`; `notYourShare` → `ingen andel`. ("settled / you owe / you're owed / your share settled / not your share").
- Whole row is a **button** → Card + Avatar, opens Entry detail.

### 2.1 Hem `/` — Overview

`/` is **always** the multi-household Overview (ADR-0021), regardless of household count. The single-book dashboard heritage moved to **Hushållet** (§2.2). Fully specified in **multi-household-overview-addendum** (single- vs mixed-currency frames, `Vänner`, `Arkiverade`) and **home-hushallet-addendum** (always-Overview, no single-household collapse) — component is `src/components/overview.tsx`. Thin entry:

**Components** — header eyebrow `Översikt` + title `Ditt hushåll` (one book) / `Dina hushåll` (multiple); dismissible **add-number banner** (ADR-0026); roll-up **hero** (single-currency: net + sub; mixed-currency: descriptive, never summed); `Hushåll` section = one `HouseholdCard` per active book (rounded-square bubble, name, member names, right net + sub) with `+ Nytt hushåll`; `Vänner` affordance card → add-friend sheet; `Arkiverade` section (ADR-0016, owner-only `Återställ`).

- **Add-number banner** (ADR-0026): shown when you're owed in ≥1 book and have no number saved; button `Lägg till nummer` → `/profil`; dismissal persisted in `localStorage` so it never nags.
- **Single vs multi book:** one active book → single `HouseholdCard` shows an `Öppna` chip (net already fills the hero); a book card sets it active and navigates to `/hushallet` / `/hushall/$id`.
- **Mixed currencies:** the client detects >1 distinct currency and switches to the descriptive `Din ställning` hero — **no cross-currency arithmetic anywhere**.

**Data** — `GET /households` → `HouseholdListItemDto[]` (`id, name, currency, memberNames, netMinor, netLabel`, archived fields + `isOwner`). Roll-up derived per the addendum. All server-derived (ADR-0006).

**Copy** (sv → en):
- Eyebrow `Översikt` ("overview"); title `Ditt hushåll` / `Dina hushåll`.
- Add-number banner: title `Lägg till ditt nummer`; body `Du har pengar att få. Med ditt nummer sparat kan andra Swisha dig direkt när ni gör upp.`; button `Lägg till nummer`; dismiss aria `Dölj`.
- Single-currency hero: `Du ska få totalt` / `Du är skyldig totalt` / `Allt är kvitt`; sub `i {namn} · {n} öppna poster` (one book) / `över {n} aktiva hushåll · {n} öppna poster`.
- Mixed-currency hero: `Din ställning` ("where you stand"); `Du ska få i {n} hushåll` / `Du är skyldig i {n} hushåll`; note `Olika valutor — summeras inte. Öppna ett hushåll för dess saldo.`
- Sections `Hushåll` / `Vänner` / `Arkiverade`; card sub `du ska få` / `du är skyldig` / `kvitt`; `Öppna`; `+ Nytt hushåll`; mixed-currency footnote `Saldot per hushåll är i hushållets egen valuta. Inget kombinerat totalbelopp visas när valutorna skiljer sig.`
- Friends card: `Lägg till en vän` + `Bjud in via nummer eller mejl`.
- Archived: row `Arkiverad {datum} · {valuta}`, chip `Återställ`, note `Bara ägaren ser återställ-knappen.`, toasts `{namn} återställd` / `Kunde inte återställa hushållet`.

**States** — Loading: skeleton. Zero households → `NoHouseholdState` (auto-opens the create sheet). Empty archived → section hidden. Error: retry card.

### 2.2 Hushållet `/hushallet`, `/hushall/$id`

The merged dashboard + ledger for the **active** household in one scroll (ADR-0021; supersedes the standalone `/ledger` Loggbok). Fully specified in **home-hushallet-addendum** §2.2 — component is `src/components/household-book.tsx`, driving both the tab (active book) and the `/hushall/$id` drill-in.

**Components** (top to bottom)
- **Net hero** — uppercase label, mono net (color by sign), sub `{n} öppna poster i {namn}`.
- **`Saldon`** — per-person balance rows: `MemberAvatar` (circle) + name + relation gloss; each owed/owing row carries a prominent **`Gör upp`** chip → Settle-up sheet for that person; a square person shows a muted `Kvitt` chip + `allt är kvitt` and no action (`opacity .6`). Whole row still opens the sheet (ADR-0021: per-person settle, no aggregate "settle all").
- **`På gång`** (MOBILE) — horizontally scrollable dashed ghost `Card`s (`GhostCard`); on **desktop this moves to the right rail**. Clickable → Recurring detail (see ambiguities).
- **`Loggbok`** — section header `Loggbok` + `{n} poster`; a **search** input (`Sök i loggboken`, clear `Rensa sök`); **filter pills** — `ToggleGroup` single-select `Alla / Utgifter / Repeat` → `filter` = `all | expense | recurring` (**no `Lån` pill** — ADR-0020); then day-grouped `<EntryRow>` buttons (group header `Idag` / `Igår` / `{d} {mån}`).

**Data** — `GET /households/{id}/summary` (`overallNet`, `netLabel`, `openCount`, `people[]` incl. `relation`, `upcoming[]`) + `GET /households/{id}/entries?filter={all|expense|recurring}&sort=date_desc`, grouped client-side by `daysUntil(date)` (0→`Idag`, −1→`Igår`, else `{d} {mån}`). All derived server-side.

**Copy** — net labels `Du ska få totalt` / `Du är skyldig totalt` / `Allt är kvitt`, sub `{n} öppna poster i {namn}`; sections `Saldon`, `På gång`, `Loggbok` (`{n} poster`); relation `är skyldig dig` / `du är skyldig` / `kvitt`; `allt är kvitt`; chips `Gör upp` / `Kvitt`; upcoming `Bokförs {datum} · {när}` + `din del`; search `Sök i loggboken`, `Rensa sök`; filters `Alla / Utgifter / Repeat` ("All / Expenses / Repeat"); `Idag` / `Igår`. Deliberate label variants remain: Add-sheet type is `Återkommande`, ledger filter + mobile tab is `Repeat`, desktop nav is `På repeat`.

**States** — Loading: skeletons. No active book → `<Navigate to="/">`. Per-filter empty (`EMPTY_COPY`): all → `Inga poster än`, expense → `Inga utgifter än`, recurring → `Inga repeat-poster än`. Search no-hits → `Inga träffar för "{query}"`. Settled rows render at `opacity .5`. Error: retry card.

### 2.3 På repeat `/recurring`

**Components**
- Title **"På repeat"** + subtitle.
- **Two summary tiles** — two `Card`s (right uses `--accent`/soft bg + accent text): left `delat / månad` (recTotal), right `din del / månad` (recShare). Both mono.
- **Recurring template cards** — `Card` + `Progress`. `↻` icon, title, `{kadens} · {du betalar|namn betalar}` sub, amount + `/ mån` or `/ 2 v`. **Progress = elapsed share of current cycle** = `clamp(1 − daysLeft/cycleLen, 0.04, 1)` (cycleLen: monthly 30 / biweekly 14 / weekly 7). Footer: next-post accent label (`Bokförs {när}` / `Pausad` / `Avslutad`), overlapping member avatars, split label. Two buttons: **"Detaljer"** (chip) → Recurring detail; **"Pausa" / "Återuppta"** (outline) toggles `active`. Paused card `opacity .55`, progress `0%`.
- **End-dated templates** (recurring-end-date spec) — a template with an `EndDate` stops posting once its cursor passes it; once **ended** (`Ended` derived, never stored) the card reads `Avslutad`, de-emphasised, and sorts below active/upcoming. No auto-delete, no clawback.
- **Footer explainer** (muted caption).

**Data** — `GET /households/{id}/recurring` → templates (active + paused + ended) with server-computed `monthlyNormalized`, `yourShare`, `cycleProgress`, `nextPostDate`, `daysUntil`, `payerName`, `splitMode`, `contributingAvatars[]`, `active`, `endDate`, `ended`. `recTotal` = Σ monthly-normalized active; `recShare` = your monthly-normalized share (API normalizes monthly ×1 / biweekly ×2 / weekly ×4 consistently — ambiguity #14). `Ended = endDate != null && nextPostDate > endDate` (recurring-end-date spec).

**Copy** — subtitle `Återkommande kostnader bokför sig själva — ingen månatlig inmatning.` ("Recurring costs post themselves — no monthly data entry."); tiles `delat / månad`, `din del / månad`; cadence `Varje månad / Varannan vecka / Varje vecka`; units `/ mån`, `/ 2 v`; split labels `Delas lika / Egna procent / Egna belopp` ("split equally / own percentages / own amounts"); `Detaljer`; `Pausa` / `Återuppta`; status `Bokförs {när}` / `Pausad` / `Avslutad`; footer `Varje period bokför Settl posten i loggboken och delar om den — ändra delningen när som helst, det gäller framtida perioder.`

**States** — Loading: tile + 2 card skeletons. Active vs paused (dimmed, 0% bar, `Pausad`, `Återuppta` button) vs ended (`Avslutad`, de-emphasised). Pause/resume fires toast + refetch (`{titel} återupptagen` / `{titel} pausad — inga fler autobokföringar`). Empty → `Inget på repeat än`. Error: retry card (`Något gick fel. Försök igen.`).

### 2.4 Notiser `/activity`

**Components**
- Title **"Notiser"** + subtitle `Settl säger bara till när något händer.`
- **Nudge cards** — `Card` + secondary `Button`(s). Accent dot, title, `when` timestamp (right, muted), body (tone-dependent), 1–2 action pills `Visa` / `Gör upp`. `Visa` → related entry/recurring detail; `Gör upp` → Settle-up sheet for the payer (shown only when payer ≠ you).
- **Empty state** — centered bell icon + `Allt lugnt` + calm one-liner (no trigger listing; that lives in the triggers card).
- **Triggers card** (always shown) — eyebrow `Du hör av oss när` over a 3-row icon list of the event triggers. Replaces the old redundant footer sentence with a scannable reference.

**Data** — `GET /households/{id}/nudges` → derived on read (ADR-0007, no storage) from 3 triggers, **in this order**: (1) active recurring `daysUntil(next) <= 5`; (2) an entry (expense or recurring post), unsettled, `amount >= 1500 kr` within last 7 days; (3) pairwise net `abs >= 750 kr`. Each nudge: `{ kind, title, body, when, actions[] }`. Trigger (3) fires on **crossing**, not while-over: the API replays the pair's net over `Entry.CreatedAt` / `Settlement.SettledAt` and emits only if the most recent upward crossing of 750 kr is within 7 days — no storage (ADR-0023, resolves ambiguity #6).

**Tone** — the per-user gentle/direct chooser was removed; in-app nudges use the **direct voice** (ambiguity #18, reminder-delivery). The `gentle` copy variants below are retired — kept only for provenance.

**Delivery (out-of-app)** — beyond in-app rendering, nudges are also delivered as a **daily email digest** via Resend (ADR-0024 + reminder-delivery spec): at most one email per user per day, only when they have ≥1 pending nudge, **opt-in off by default** (toggled on `/profil`). A one-click unsubscribe forces it off. A persisted, de-duplicated emitted-nudge log keys off each nudge's derivable identity (`recurringDue:{id}:{date}` / `bigExpense:{entryId}` / `balance:{memberId}:{crossedOn}`). This is server-side only; no in-app UI beyond the profile switch.

**Copy** (all sv):
- Title `Notiser` ("Notifications"); subtitle `Settl säger bara till när något händer.` ("Settl only speaks up when something happens.").
- Recurring-due title: direct `{titel} dras {när}` ("{title} is charged {when}"); gentle `{titel} bokförs {när}` ("{title} posts {when}").
- Recurring-due body: direct `Din del är {belopp}. Den bokförs automatiskt.`; gentle `Din del ({belopp}) hamnar i loggboken automatiskt — inget att göra.`.
- Big-expense title `Stor utgift: {titel}` ("Big expense: {title}").
- Big-expense body: direct `{namn} la till {belopp}. Din del är {belopp} — gör upp när du kan.`; gentle `{namn} la till {belopp}. Din del är {belopp}. Ingen brådska.`.
- Balance title: direct `Du är skyldig {namn} {belopp}` / `{namn} är skyldig dig {belopp}`; gentle `Er nota med {namn} växer` ("your tab with {name} is growing").
- Balance body: direct `Saldot passerade 750 kr — dags att göra upp.`; gentle `Ert saldo passerade 750 kr. Kanske ett bra tillfälle att göra upp.`.
- Actions `Visa` / `Gör upp` ("View / Settle up").
- Empty (mobile) `Allt lugnt` + `Inga notiser att ta tag i just nu.`; empty (desktop rail) `Allt lugnt. Du hör av oss vid en stor utgift, ett saldo som blir stort eller en återkommande kostnad som snart bokförs.`.
- Triggers card `Du hör av oss när` over: `En stor utgift läggs till` / `Från 1500 kr och uppåt.`; `Ett saldo blir stort` / `När det passerar 750 kr.`; `En återkommande kostnad närmar sig` / `Några dagar innan den bokförs.`.

**States** — Loading: 2 card skeletons. `hasReminders` (cards) vs `noReminders` (empty text). On desktop the same nudges also render in the right rail (more compact). Error: retry card.

### 2.5 Dina hushåll (overlay `?sheet=households`)

**Components** — responsive sheet (Drawer mobile / Dialog desktop) titled **"Dina hushåll"** + subtitle `En bok per hushåll — du kan vara med i flera.`. Household rows = `Card` buttons: rounded-square bubble, name, member names comma-joined, right net amount + sub (`du ska få` / `du är skyldig` / `kvitt`); active household outlined in accent. **`Arkiverade`** section (owner-only `Återställ`, ADR-0016). **Invite section** (`Bjud in till {namn}`): email input, `Skicka`, pending-invite list; plus `Från dina kontakter` (invitable saved contacts with `Med` / `Väntar` chips + `Bjud in`). Bottom **"+ Nytt hushåll"** dashed outline `Button` → the create-household sheet.

**Data** — `GET /households` → household list items; invites via the household invite endpoints (ADR-0011); invitable contacts via `GET /households/{id}/invitable-contacts` (ADR-0019). Restore via `POST /households/{id}/restore` (ADR-0016).

**Copy** — `Dina hushåll`; `En bok per hushåll — du kan vara med i flera.`; row sub `du ska få / du är skyldig / kvitt`; `Arkiverade`, `Bara ägaren ser återställ-knappen.`, `Arkiverad {datum}`, `Återställ`; `Bjud in till {namn|hushållet}`, placeholder `namn@exempel.se`, `Skicka`, `— väntar på svar`, `Från dina kontakter`, chips `Med` / `Väntar`, `Bjud in`; `+ Nytt hushåll`. Toasts: `Bytte till {namn}`; `Skriv en e-postadress`; `Inbjudan skickad till {email}`; `Inbjudan skapad men mejlet kunde inte skickas till {email} — försök igen senare`; `Inbjudan skickad till {namn}`; `{namn} återställd` / `Kunde inte återställa hushållet`.

**States** — Selecting a household switches the active household, closes the sheet, toast `Bytte till {namn}`. Loading: row skeletons.

### 2.5b Nytt hushåll (overlay `?sheet=newHousehold`)

**Components** — responsive sheet titled **"Nytt hushåll"** + subtitle `En egen bok för stället eller gänget — bjud in andra efteråt.`. Single name `Input` (placeholder `Sommarstugan, Kollektivet, Resgänget…`). Save button **"Skapa hushåll"**. There is **no member-adding UI** — the book is created empty (just you) and others are invited afterward from the household's switcher/management sheets (ADR-0011/ADR-0019).

**Data** — `POST /households` `{ name }`; the acting user is the owner + sole member server-side. → 201 household.

**Copy** — `Nytt hushåll`; `En egen bok för stället eller gänget — bjud in andra efteråt.`; placeholder `Sommarstugan, Kollektivet, Resgänget…`; `Skapa hushåll`; validation toast `Ge hushållet ett namn först`; success toast `{namn} skapat — bjud in andra från hushållets inställningar`; error `Något gick fel. Försök igen.`

**States** — On save: create, switch active household to the new one, close sheet, toast.

### 2.6 Ny post (overlay `?sheet=add`)

Also serves **edit** (`?sheet=edit&id=…`, `?sheet=editRecurring&id=…`) — same form, prefilled (ADR-0018). Governed by the unified-add-entry spec, add-entry-addendum, ADR-0018 (split presets + editing), ADR-0020 (no IOU type), and recurring-end-date. Only two types exist — `Utgift` and `Återkommande` (the old `Lån` tab is gone).

**Components**
- Title **"Ny post"** (create) / **"Redigera post"** / **"Redigera återkommande"** (edit).
- **Type picker** — `Tabs` (pill): `Utgift / Återkommande` (`EntryTab = 'expense' | 'recurring'`).
- **Amount input** — `Input` mono borderless, 38px right-aligned, placeholder `0,00`, `inputMode="decimal"`, accepts comma decimal; `kr` suffix.
- **Title input** — placeholder by type: expense `Mat, middag, biljetter…`, recurring `Hyra, internet, Spotify…`.
- **Payer + split** — `ToggleGroup` `Vem betalade` (payers) + `ToggleGroup` `Delning`: `Lika / Allt / % / kr` (`SplitMode = 'equal' | 'whole' | 'percent' | 'amount'`). `Allt` puts the **whole amount on one person** (payer share = 0) — this replaces the removed IOU type (ADR-0020); it reveals a `Vem står för hela beloppet?` picker. Percent/amount reveal a numeric `Input` per member with unit (`%` / `kr`). **Live balance hint** below rows, color-coded (`--success` when balanced, destructive when off).
- **Recurring branch** — `ToggleGroup` `Upprepas`: `Varje månad / Varannan vecka / Varje vecka` + date `Input` `Bokförs först` + **`Slutar` selector** (`Aldrig / Datum / Efter antal`; `Datum` reveals a date input, `Efter antal` a number input with `gånger` suffix, placeholder `12`) — recurring-end-date spec + explainer.
- **Save button** — label by type/mode; `opacity .45` until amount > 0 (still clickable → validation toast).

**Data** — form: `{ type, title, amount, paidBy, splitMode, vals{}, cadence, date, endMode, endValue }`; members from active household. Save → `POST /households/{id}/entries` (expense) or `POST /households/{id}/recurring` (create); `PUT /entries/{id}` / `PUT /recurring/{id}` (edit). **API freezes integer shares** with deterministic remainder distribution (ADR-0007); `Slutar` count/date resolves to a single `EndDate` server-side (recurring-end-date); amount is **integer minor units (öre)**; category assigned server-side from the title (ADR-0012).

**Copy** — titles `Ny post` / `Redigera post` / `Redigera återkommande`; types `Utgift / Återkommande` ("Expense / Recurring"); `0,00`; placeholders above; `Vem betalade` ("who paid"); `Delning` ("split"), `Lika / Allt / % / kr`; whole-mode `Vem står för hela beloppet?` + hints `{namn} står för hela beloppet` / `En person står för hela beloppet`; equal hints `{belopp} var` / `Alla betalar lika mycket`; percent hint `{n} % av 100 % fördelat`; amount hint `{belopp} av {belopp} fördelat`; `Upprepas`; `Bokförs först`; `Slutar` (`Aldrig / Datum / Efter antal`), `gånger`; recurring explainer `Settl bokför den i loggboken varje period och delar om automatiskt. Ställ in en gång, glöm den sen.`; save labels `Lägg i loggboken` (expense) / `Sätt på repeat` (recurring) / `Spara ändringar` (edit).

**States / validation** (on save, surfaced as **toasts**; keep the inline color-coded hint too):
- amount ≤ 0 → `Ange ett belopp först`.
- percent sum ≠ 100 (±0.5) → `Procenten måste bli 100`.
- amount split sum ≠ total (±0.05 kr) → `Delningen måste bli {belopp}`.
- no split members → `Lägg till någon att dela med`.
- end-date issues → `Välj ett slutdatum` / `Slutdatumet kan inte vara före första bokföringen` / `Ange hur många gånger den ska bokföras`.
- Success: create expense `Tillagd i loggboken`; create recurring `På repeat — bokförs först {datum}`; edit `Ändrad`. On success sheet closes, form resets, list updates; new recurring jumps to `/recurring`.
- Default title when blank: `Utan titel`.
- Loading (submit): disable button, spinner. Error (API): toast `Något gick fel. Försök igen.` + keep form.

### 2.7 Entry detail (overlay `?sheet=entry&id=`)

**Components** — `Badge` type (`Utgift` / `På repeat`) + date; title, amount (mono 28px), meta (`Du betalade`/`{namn} betalade` + split label); a tappable **category** control (ADR-0012: pick from the 12 groups + `Other`); optional accent auto-post note `Bokförd automatiskt från "{titel}"`; split share rows (`Card` + `Avatar`, name + tag, mono share); a **⋯ menu** (`Redigera` / `Ta bort`, ADR-0018); toggle settle `Button` (primary when open, chip when settled); footer caption. (No IOU badge or "informal loan" note — ADR-0020.)

**Data** — `GET /entries/{id}` → `{ type, date, title, amount, paidBy, category, splitMode, shares[], recurringId, templateTitle }` with **frozen shares** and **derived** settled state (from settlement events). Settle → `POST /entries/{id}/settlements`; reopen → `DELETE` the settlement; edit → `PUT /entries/{id}` (incl. `category`); delete → `DELETE /entries/{id}`. Locked once a settlement touches it — reopen before editing/deleting (ADR-0007 / ADR-0018).

**Copy** — badges `Utgift` / `På repeat`; split labels `Delas lika` / `Egna procent` / `Egna belopp`; `Du betalade` / `{namn} betalade`; `Bokförd automatiskt från "{titel}"`; row tags `· betalade` / `· skyldig`; category aria `Kategori: {label} — tryck för att ändra`; ⋯ menu `Redigera` / `Ta bort`, locked note `Låst — öppna igen för att ändra eller ta bort.`; locked box `Posten är låst` + `En uppgörelse rör den här posten. Öppna igen innan du ändrar eller tar bort — då rörs bara den här posten.`; toggle `Markera som reglerad` / `Reglerad ✓ — tryck för att öppna igen`; footer `Betala varandra hur ni vill — markera sen som reglerad här.`; delete confirm `Ta bort posten?` + `Det här ändrar saldot för alla som var med` + `"{titel}" · {belopp} tas bort ur loggboken. Du kan ångra direkt efteråt.`; toasts `Reglerad — snyggt` / `Öppnad igen` (errors `Kunde inte öppna igen` / `Kunde inte reglera` / `Kunde inte ändra kategori`).

**States** — Loading: skeleton. Settled vs open swaps button style/label + locks edit/delete. Error: retry.

### 2.8 Gör upp (overlay `?sheet=settle&person=`)

**Components** — title **"Gör upp med {namn}"**; net summary (label + mono 32px colored amount); contributing entry list (`Card` rows: title, date, **signed** amount — `+` positive / `−` U+2212 minus negative, colored); explainer; **"Betala med Swish"** action when the API returns `swishPay` (debtor, SEK, creditor has a number) — a tap-through deep link on mobile, a QR of the same `uri` on desktop (swish-settlement-payments spec); **"add your number" nudge** shown to the creditor (`owesYou`) who has no number saved — an accent `Card` with a `Lägg till nummer` button → `/profil` (ADR-0026); **"Markera allt som reglerat"** primary `Button`; a read-only **`Tidigare uppgörelser med {namn}`** history list of past settlement events (settlement-history spec).

**Data** — `GET /households/{id}/settle-preview?person={memberId}` → `{ net, netLabel, memberName, entries[]{ id, title, date, signedAmount }, swishPay?{ uri, amountMinor } }` (`swishPay` non-null only for the debtor, SEK, when the creditor saved a number — the URL is built server-side, ADR-0006). The creditor-side nudge is derived client-side from `netLabel === 'owesYou'` + the current member's empty number. Confirm → `POST /households/{id}/settlements` with the pair → records **one first-class settlement event** closing all open pair debts; entries with no remaining open debt become settled (derived).

**Copy** — `Gör upp med {namn}`; net label `{namn} är skyldig dig` / `Du är skyldig {namn}` / `Allt är kvitt`; explainer has two variants — with `swishPay`: `Betala först — bocka sedan av. Settl bokför inget automatiskt.`; otherwise `Betala som ni brukar — Swish, kontanter, banköverföring. Settl håller bara boken i ordning.` (note: **Swish**, not Venmo — SEK context); Swish action `Betala med Swish` + desktop caption `Skanna med Swish-appen`; add-number nudge `Lägg till ditt nummer så kan {namn} Swisha dig med rätt belopp när ni gör upp.` + button `Lägg till nummer` (ADR-0026); primary button `Markera allt som reglerat` ("mark everything as settled"); toast `Uppgjort med {namn} — rent bord` ("settled with {name} — clean slate") / error `Kunde inte göra upp`; history header `Tidigare uppgörelser med {namn}`, empty `Inga tidigare uppgörelser än`, row sub `{Du|namn} gjorde upp · {n} post/poster` (settlement-history spec).

**States** — Loading: skeleton. All square → `Allt är kvitt`, empty list, button hidden/disabled. Error: retry.

### 2.9 Recurring detail (overlay `?sheet=recurring&id=`)

**Components** — `Badge` `På repeat` (or `Avslutad` when ended) + cadence label; title + amount (mono 28px); **next-post dashed `Card`**: `↻`, `Nästa: {titel} — {datum}` + `Bokförs automatiskt {när}` (`· slutar {datum}` when an end-date is set) + your share + `din del` (paused / ended variants below); **"Hur den delas"** split rows (`Card` + `Avatar`, tag `· betalar`) + caption; **"Tidigare perioder"** posted-entry list (title, status `reglerad`/`öppen`, mono amount) — shown only if posts exist; a **⋯ menu** (`Redigera` / `Ta bort`, ADR-0018 — delete disabled once posts exist, pause instead); pause/resume outline `Button`.

**Data** — `GET /recurring/{id}` → template `{ title, amount, cadence, nextPostDate, payer, splitMode, shares[], yourShare, active, endDate, ended }` + `postedEntries[]` (linked via `recurringId`: `{ id, title, amount, settled }`). Pause/resume → `PATCH /recurring/{id} { active }`; edit → `PUT /recurring/{id}` (incl. termination); delete → `DELETE /recurring/{id}` (only if no posts).

**Copy** — badge `På repeat` / `Avslutad`; next-post `Nästa: {titel} — {datum}` + `Bokförs automatiskt {när}`; ended `Avslutad — {datum}` + `Inga fler bokföringar — redigera slutdatumet för att återuppta`; paused `Pausad — ingen kommande bokföring` + `Återuppta för att schemalägga nästa period`; `Hur den delas` + caption `Ändrad delning gäller framtida perioder.`; `Tidigare perioder` with statuses `reglerad` / `öppen`, note `De bokförda posterna är vanliga poster — ta bort dem var för sig i loggboken.`; ⋯ menu `Redigera` / `Ta bort` (disabled note `Har bokförda perioder — pausa i stället, så finns historiken kvar.`); button `Pausa autobokföring` / `Återuppta autobokföring`; toasts `{titel} återupptagen` / `{titel} pausad` / `{titel} borttagen` (error `Något gick fel. Försök igen.`).

**States** — Loading: skeleton. Active vs paused (paused next-post variants) vs ended (`Avslutad`). No posts → hide "Tidigare perioder" + allow delete. Error: retry.

### 2.10 Profil `/profil`

Fully specified in **profile-addendum** + **ADR-0026** (single number) + **reminder-delivery** (email opt-in). Part of `<AppShell>`. Route `src/routes/profil.tsx`; reached via the account avatar (mobile header) / sidebar footer.

**Components** — back button + `Profil` uplabel; **avatar block** (78px `MemberAvatar` circle; `Byt emoji` / `Ta bort` or `Välj profilbild` → `?sheet=avatar` emoji picker, avatar-personalization spec); `Namn` `Input` (caption `Så här syns du i loggboken.`); **`Ditt nummer` `(valfritt)`** phone `Input` (`+46`, powers Swish; unverified, ADR-0026 + swish spec); `Konto` card (read-only `E-post` + `Din inloggning`); **`Påminnelser via e-post`** opt-in `Switch` (off by default, reminder-delivery); `Spara` / `Sparar…`; `Logga ut`. The gentle/direct tone chooser and `Byt lösenord` are intentionally **not** present (ambiguity #18; password change omitted from this build).

**Data** — `GET /me` → `MeDto` (name, email, avatarColor, avatarEmoji, phone, nudgeEmailsEnabled). Save → `PUT /me`. Sign out → auth logout (ADR-0005). Phone normalized E.164 server-side.

**Copy** — `Profil`, `Tillbaka`; `Byt emoji` / `Ta bort` / `Välj profilbild`; `Namn`, `Så här syns du i loggboken.`, `Ange ditt namn.`; `Ditt nummer` `(valfritt)`, placeholder `73 555 12 34`, helper `Andra i hushållet kan Swisha dig direkt när ni gör upp. Sparas som det är — inte verifierat.`; `Konto`, `E-post`, `Din inloggning`; `Påminnelser via e-post` (on: `Ett dagligt mejl när du har notiser att ta tag i. Aldrig mer än ett per dag.` / off: `Du får inga påminnelser via e-post. Notiser visas fortfarande i appen.`); `Spara` / `Sparar…`, `Logga ut`; toasts `Profilbild uppdaterad`, `Profilen sparad`, `Något gick fel. Försök igen.`

### 2.11 Kontakter `/kontakter`

Fully specified in **ADR-0019** + **contacts-addendum**. Saved contacts (Member↔Member on invite accept) + pending outgoing invites; **no own-number input** (moved to Profil, ADR-0026). Route `src/routes/kontakter.tsx`; reached via the account menu. SMS provider is deferred (ADR-0019).

**Components** — title `Kontakter` + subtitle `Sparade vänner och familj — lägg till via telefonnummer.`; accepted-contact rows (`MemberAvatar` circle + name + shared-book count); `Väntar på svar` pending section (channel `SMS-inbjudan` / `E-postinbjudan`, `{via} skickad {datum}`, badge `Väntar`); `+ Lägg till kontakt` → `add-contact-sheet` (blind number/email invite; privacy note that registration is never revealed).

**Data** — `GET /contacts` + `GET /contacts/pending`; `POST /contacts/invites { channel, phone?/email? }` (contacts-addendum — proposed contract; SMS vendor deferred).

**Copy** — `Kontakter`; `Sparade vänner och familj — lägg till via telefonnummer.`; empty `Inga kontakter än. Lägg till vänner och familj via telefonnummer, så slipper du skriva in dem varje gång du delar ett hushåll.`; shared-count `Ingen delad bok än` / `I 1 hushåll med dig` / `I {n} hushåll med dig`; `Väntar på svar`, `SMS-inbjudan` / `E-postinbjudan`, `{via} skickad {datum}`, `Väntar`; `+ Lägg till kontakt`.

### 2.12 Statistik `/statistik`

Fully specified in **household-statistics spec**. Per-person monthly contribution line/bar chart for one household (who paid what, trailing 12 months), server-aggregated. Route `src/routes/statistik.tsx`; the 4th nav tab (took the old Aktivitet slot). Uses shadcn `chart` (Recharts) with per-member colors.

**Data** — `GET /households/{id}/stats/contributions?from=&to=` → `ContributionStatsDto` (`Currency`, `Members[]`, `Buckets[]` monthly, zero-filled). Aggregates `Expense` + `RecurringPost` by `PaidByMemberId` × month-of-`Date`. Server-side only (ADR-0006).

**Copy** — `Statistik`; subtitle `Vem la ut vad, månad för månad.`; empty `Inget att visa än. När någon lägger till utgifter dyker det upp här — vem som la ut vad, månad för månad.`; footer `Visar vem som lagt ut hur mycket per månad, de senaste 12 månaderna. Bara vem som betalat — inte vem som är skyldig vad.`

### 2.13 Hushåll (manage) `/household`

Fully specified in **ADR-0016** (ownership/archival) + **ADR-0022** (delete empty) + **household-ownership spec**. Members roster with owner (`Ägare`) actions. Route `src/routes/household.tsx`; reached via the account menu (`Hantera hushåll`). Confirm flows are the `leaveHousehold` / `transferOwnership` / `archiveHousehold` / `deleteHousehold` sheets.

**Components** — back button + `Hushåll` uplabel; member count `{n} medlem(mar) · {valuta}`; `Medlemmar` list (self row `Du`, owner badge `Ägare`); owner buttons `Överför ägarskap` / `Arkivera hushåll` / `Ta bort hushåll` (delete disabled unless empty — ADR-0022); member button `Lämna hushåll`; footer variants by role.

**Data** — household detail + membership; `POST /households/{id}/restore`, archive, transfer-ownership, leave, delete endpoints (ADR-0016 / ADR-0022).

**Copy** — `Hushåll`, `Tillbaka`; `{n} medlem` / `{n} medlemmar`; `Medlemmar`, `Du`, `Ägare`; `Överför ägarskap` / `Arkivera hushåll` / `Ta bort hushåll` / `Lämna hushåll`; footers `Du äger hushållet. Tomma hushåll kan tas bort helt.` / `Du äger hushållet. Ett hushåll med poster kan bara arkiveras, inte tas bort.` / `Bara ägaren ({namn}) kan arkivera hushållet.` (See the household-ownership spec + the leave/archive/delete/transfer sheets for full confirm-flow copy.)

---

## 3. Shared shell

Single responsive `<AppShell>` around the router `<Outlet>`. `wide = innerWidth >= 980`.

**Navigation — custom component (no stock shadcn):** the nav is **four tabs — `Hem / Hushållet / På repeat / Statistik`** (ADR-0021 set Hem=Overview + Hushållet; household-statistics took the old `Aktivitet` slot). Notiser (`/activity`) has **no tab** — it's reached via the mobile header bell and the desktop right rail.
- **Desktop = left sidebar** (sticky, 220px): `Settl` wordmark + tagline `Hushållets delade anteckningsbok`; household switcher button; vertical nav with active-dot — `Hem / Hushållet / På repeat / Statistik`; primary **"+ Ny post"** button (opens Add sheet); helper text `Interaktiv prototyp — lägg till en post, gör upp, pausa en återkommande kostnad eller byt hushåll.`; footer holds the theme toggle (+ dev user switcher).
- **Mobile = bottom tab bar** (fixed, 5-slot grid `1fr 1fr 66px 1fr 1fr`): `Hem`, `Hushållet`, center raised circular **"+" FAB** (accent, 52px, translateY −12px, accent shadow → Add sheet), `Repeat`, `Statistik`. Active tab = dot + foreground color; inactive = muted. Screen order fixed: Home, Hushållet, (add), Repeat, Statistik.
- **Mobile top header** (sticky, title-integrated): the active household name is the **screen title** on the left (fallback `Settl`), tap opens the **Dina hushåll** sheet. The right side is slim: the **bell** (`aria-label="Notiser"` → `/activity`, nudge dot when unread) + the acting user's **account avatar** (opens the account menu: `Profil` / `Kontakter` / `Hantera hushåll` / `Logga ut`).
- **Desktop right rail** (300px, active-household-scoped): a `På gång` section (upcoming auto-posts) + a `Notiser` section (compact nudges; empty state `Allt lugnt. Du hör av oss vid en stor utgift, ett saldo som blir stort eller en återkommande kostnad som snart bokförs.`).

**Household switcher** — the sidebar button (desktop) / header title (mobile) opens the **Dina hushåll** sheet (§2.5). Keep multi-household (many-to-many; "You" belongs to several).

**Theme / dark-mode toggle** — the six named palettes (Mint/Papper/Citrus × light/dark) are **prototype/harness controls only**; ship real light+dark via a single `--success`-augmented token set (see §7 and handoff globals.css). Place a **light/dark toggle** in the sidebar footer (desktop) and inside the household/settings drawer (mobile). Do **not** port the three named directions as user-facing options in v1 (see ambiguities). Default: follow system, persist choice.

**Dev user switcher** — not present in any export (user is always `u1 "You"`; auth is ASP.NET Identity per ADR-0005). Add a **dev-only** user switcher (gated behind `import.meta.env.DEV`) in the sidebar footer next to the theme toggle (desktop) / bottom of the household drawer (mobile), so a developer can impersonate a seed member. Not shipped in production (see ambiguities).

**Overlays** — one `<ResponsiveSheet>` wrapper: `Dialog` (desktop, centered, radius 24px, width `min(480px,100%)`, scrim) / `Drawer`/vaul (mobile, top radius 26px, slide-up, 36×4px grip handle). Driven by the `sheet` search param; only one at a time; scrim/handle close.

**Toasts** — `Sonner`, single toast, auto-dismiss ~2600ms, bottom-center (offset ~28px desktop / ~104px mobile to clear the tab bar).

---

## 4. Flows

### Add entry (2 types, 4-mode split editor + live validation)
1. Sidebar "+ Ny post" / mobile "+" FAB → Add sheet.
2. `Tabs` default **Utgift** (or `Återkommande`). Amount (comma decimal) → Save un-dims when > 0. Title.
3. Pick payer (`Vem betalade`), then `Delning` mode:
   - **Lika (equal):** read-only per-member share text; hint `{belopp} var` (or `Alla betalar lika mycket` when amount empty).
   - **Allt (whole):** the whole amount on one person (payer share = 0) — this replaces the removed IOU type (ADR-0020); pick the person via `Vem står för hela beloppet?`.
   - **% (percent):** numeric `%` input per member; hint `{n} % av 100 % fördelat`, green when sum = 100 (±0.5).
   - **kr (amount):** numeric `kr` input per member; hint `{belopp} av {belopp} fördelat`, green when sum = total (±0.05 kr).
4. Recurring adds `Upprepas` cadence + `Bokförs först` date + `Slutar` (`Aldrig / Datum / Efter antal`, recurring-end-date).
5. Save validates → toast on failure (`Ange ett belopp först` / `Procenten måste bli 100` / `Delningen måste bli {belopp}` / `Lägg till någon att dela med` / end-date errors); on success API **freezes integer shares** + assigns a category (ADR-0012), the list updates, sheet closes, success toast, new recurring jumps to `/recurring`. Editing an existing entry/template runs the same form (`Spara ändringar`).

**Validation rules:** percent tolerance ±0.5; amount tolerance ±0.05 kr; amount must be > 0. Inline hint is live + color-coded; hard errors are toasts. All final validation authoritative in the API (ADR-0006).

### Settle up ("Markera allt som reglerat" + per-pair)
- A Hushållet `Saldon` row (`Gör upp` chip) or a nudge `Gör upp` → Settle-up sheet (§2.8). Shows net + signed contributing entries, optional Swish pay, and past-settlement history.
- **"Markera allt som reglerat"** → one settlement event closes all open debts in that pair; entries fully closed flip to `reglerad` (derived, never a boolean); toast `Uppgjort med {namn} — rent bord`.
- Per-pair semantics: settling with one person closes only that pair's debts — a 3-way expense stays open for the third person until their pair is settled too.

### Entry detail settle / reopen
- Row → Entry detail (§2.7). **"Markera som reglerad"** → settlement event, toast `Reglerad — snyggt`, button becomes `Reglerad ✓ — tryck för att öppna igen`.
- Tap again → reopen (delete settlement), toast `Öppnad igen`. Entry is locked while a settlement touches it — reopen before editing (ADR-0007).

### Recurring pause / resume
- `/recurring` card or Recurring detail → **"Pausa"/"Pausa autobokföring"** → `active=false`, card dims + `Pausad` + 0% bar, toast `{titel} pausad — inga fler autobokföringar` (card) / `{titel} pausad` (detail).
- **"Återuppta"** → `active=true`, toast `{titel} återupptagen`. Pausing stops posting without deleting history; posting itself runs in the API's hosted background service with startup catch-up (ADR-0007).

### Nudge actions
- Nudges derived on read (§2.4). `Visa` → related entry or recurring detail. `Gör upp` → Settle-up sheet for the payer (only when payer ≠ you). Recurring-due nudge → Recurring detail (`/recurring` + sheet). No daily scheduling.

---

## 5. Component install list (shadcn)

**Installed** (all present under `src/components/ui/`):
- `button`, `card` — base primitives
- `drawer` (vaul) — mobile sheets
- `dialog` — desktop sheets
- `tabs` — Add-sheet type picker (pill)
- `toggle-group` + `toggle` — split mode, payer, cadence, `Slutar` mode, ledger filter
- `avatar` — member avatars / overlapping stack
- `progress` — recurring cycle progress
- `badge` — entry type badges, filter chips (alt)
- `dropdown-menu` — account menu / household switcher
- `sonner` — toasts
- `input` — amount, title, per-member split, date, search
- `label` — form field labels
- `switch` — Profil nudge-email opt-in (reminder-delivery)
- `scroll-area` — content / upcoming rail
- `separator` — sidebar / sheet dividers
- `skeleton` — loading states
- `chart` (Recharts) — Statistik contribution chart (household-statistics spec)

Custom (not shadcn primitives, in `src/components/`): `AppShell` (sidebar + tab bar + FAB + right rail), `ResponsiveSheet` (Dialog/Drawer wrapper), `SheetRouter`, `EntryRow`, `Overview`, `HouseholdBook`, `GhostCard` (dashed upcoming), `Money` (mono sv-SE formatter), `MemberAvatar` (+ stack), `HouseholdBadge`, `AccountMenu`, `ThemeToggle`, `ScreenStates`, `DevBar`, and the per-screen sheets under `src/components/sheets/`.

---

## 6. Copy glossary (sv → en)

### Navigation & shell
| sv | en |
|---|---|
| Settl | Settl (brand) |
| Hushållets delade anteckningsbok | The household's shared notebook (sidebar tagline) |
| Hem | Home (nav — the Overview) |
| Hushållet | The book (nav — active household, ADR-0021) |
| På repeat | On repeat (desktop nav / tab title) |
| Repeat | Repeat (mobile tab / ledger filter) |
| Statistik | Statistics (nav — 4th tab, household-statistics) |
| Notiser | Notifications (Activity screen title + header bell + right-rail section) |
| + Ny post | + New entry |
| Profil / Kontakter / Hantera hushåll / Logga ut | Account menu items |
| Interaktiv prototyp — lägg till en post, gör upp, pausa en återkommande kostnad eller byt hushåll. | Interactive prototype helper (sidebar) |

### Hem (Overview) — see multi-household-overview-addendum for the full glossary
| sv | en |
|---|---|
| Översikt | Overview (eyebrow) |
| Ditt hushåll / Dina hushåll | Your household / Your households (title) |
| Du ska få totalt / Du är skyldig totalt / Allt är kvitt | You're owed in total / You owe in total / everything's square |
| i {namn} · {n} öppna poster | in {name} · {n} open entries (single-book sub) |
| över {n} aktiva hushåll · {n} öppna poster | across {n} active households · {n} open entries |
| Din ställning | Where you stand (mixed-currency hero) |
| Du ska få i {n} hushåll / Du är skyldig i {n} hushåll | You're owed in / You owe in {n} households |
| Olika valutor — summeras inte. Öppna ett hushåll för dess saldo. | Different currencies — not summed. Open a household for its balance. |
| Hushåll / Vänner / Arkiverade | Households / Friends / Archived (sections) |
| du ska få / du är skyldig / kvitt | you're owed / you owe / square (card sub) |
| Öppna | Open (single-book chip) |
| + Nytt hushåll | + New household |
| Lägg till en vän / Bjud in via nummer eller mejl | Add a friend / Invite by number or email |
| Arkiverad {datum} · {valuta} / Återställ | Archived {date} · {currency} / Restore |
| Bara ägaren ser återställ-knappen. | Only the owner sees the restore button. |
| Lägg till ditt nummer | Add your number (banner title, ADR-0026) |
| Du har pengar att få. Med ditt nummer sparat kan andra Swisha dig direkt när ni gör upp. | Add-number banner body |

### Hushållet (the book) — see home-hushallet-addendum
| sv | en |
|---|---|
| Saldon | Balances |
| {n} öppna poster i {namn} | {n} open entries in {name} |
| är skyldig dig / du är skyldig / kvitt / allt är kvitt | owes you / you owe / square |
| Gör upp / Kvitt | Settle up / Settled (chips) |
| På gång | Upcoming |
| Bokförs {datum} · {när} | Posts on {date} · {when} |
| din del | your share |
| Loggbok / {n} poster | Ledger / {n} entries |
| Sök i loggboken / Rensa sök | Search the ledger / Clear search |
| Inga poster än / Inga utgifter än / Inga repeat-poster än | Empty per filter |
| Inga träffar för "{query}" | No matches for "{query}" |

### Ledger filter (in Hushållet)
| sv | en |
|---|---|
| Alla / Utgifter / Repeat | All / Expenses / Repeat (no `Lån` — ADR-0020) |
| Idag / Igår | Today / Yesterday |

### Recurring
| sv | en |
|---|---|
| Återkommande kostnader bokför sig själva — ingen månatlig inmatning. | Recurring costs post themselves — no monthly data entry. |
| delat / månad | shared / month |
| din del / månad | your share / month |
| Varje månad / Varannan vecka / Varje vecka | Every month / Every other week / Every week |
| / mån · / 2 v | / mo · / 2 wk |
| Bokförs {när} / Pausad / Avslutad | Posts {when} / Paused / Ended (recurring-end-date) |
| Delas lika / Egna procent / Egna belopp | Split equally / Own percentages / Own amounts |
| Detaljer | Details |
| Pausa / Återuppta | Pause / Resume |
| Inget på repeat än | Nothing on repeat yet (empty state) |
| Varje period bokför Settl posten i loggboken och delar om den — ändra delningen när som helst, det gäller framtida perioder. | Recurring footer explainer |

### Nudges (Notiser)
*Tone is fixed to the **direct** voice (ambiguity #18); the `(gentle)` rows below are retired — kept for provenance only.*

| sv | en |
|---|---|
| Settl säger bara till när något händer. | Settl only speaks up when something happens. |
| {titel} dras {när} | {title} is charged {when} (direct) |
| {titel} bokförs {när} | {title} posts {when} (gentle) |
| Din del är {belopp}. Den bokförs automatiskt. | Recurring-due body (direct) |
| Din del ({belopp}) hamnar i loggboken automatiskt — inget att göra. | Recurring-due body (gentle) |
| Stor utgift: {titel} | Big expense: {title} |
| {namn} la till {belopp}. Din del är {belopp} — gör upp när du kan. | Big-expense body (direct) |
| {namn} la till {belopp}. Din del är {belopp}. Ingen brådska. | Big-expense body (gentle) |
| Du är skyldig {namn} {belopp} | You owe {name} {amount} (direct) |
| {namn} är skyldig dig {belopp} | {name} owes you {amount} (direct) |
| Er nota med {namn} växer | Your tab with {name} is growing (gentle) |
| Saldot passerade 750 kr — dags att göra upp. | Balance body (direct) |
| Ert saldo passerade 750 kr. Kanske ett bra tillfälle att göra upp. | Balance body (gentle) |
| Visa / Gör upp | View / Settle up |
| Allt lugnt / Inga notiser att ta tag i just nu. | Empty state (mobile) |
| Allt lugnt. Du hör av oss vid en stor utgift, ett saldo som blir stort eller en återkommande kostnad som snart bokförs. | Empty state (desktop rail) |
| Du hör av oss när | Triggers card eyebrow |
| En stor utgift läggs till / Från 1500 kr och uppåt. | Trigger — big expense |
| Ett saldo blir stort / När det passerar 750 kr. | Trigger — balance |
| En återkommande kostnad närmar sig / Några dagar innan den bokförs. | Trigger — recurring |

### Household switcher
| sv | en |
|---|---|
| Dina hushåll | Your households |
| En bok per hushåll — du kan vara med i flera. | One book per household — you can be in several. |
| du ska få / du är skyldig / kvitt | you're owed / you owe / square |
| Arkiverade / Arkiverad {datum} / Återställ | Archived / Archived {date} / Restore |
| Bjud in till {namn} / Skicka / — väntar på svar | Invite to {name} / Send / — awaiting reply |
| Från dina kontakter / Med / Väntar / Bjud in | From your contacts / Joined / Pending / Invite |
| + Nytt hushåll | + New household |
| En egen bok för stället eller gänget — bjud in andra efteråt. | Your own book for the place or the crew — invite others afterward. |
| Skapa hushåll | Create household |
| Bytte till {namn} | Switched to {name} |
| {namn} skapat — bjud in andra från hushållets inställningar | {name} created — invite others from the household settings |
| Ge hushållet ett namn först | Name the household first (toast) |

### Add entry
| sv | en |
|---|---|
| Ny post / Redigera post / Redigera återkommande | New entry / Edit entry / Edit recurring |
| Utgift / Återkommande | Expense / Recurring (types — no `Lån`, ADR-0020) |
| 0,00 | 0.00 (amount placeholder) |
| Mat, middag, biljetter… | Groceries, dinner, tickets… (expense) |
| Hyra, internet, Spotify… | Rent, internet, Spotify… (recurring) |
| Vem betalade | Who paid |
| Delning | Split |
| Lika / Allt / % / kr | Equal / Whole (one owes all) / % / kr |
| Vem står för hela beloppet? | Who covers the whole amount? (whole mode) |
| {namn} står för hela beloppet / En person står för hela beloppet | {name} covers the whole amount / One person covers it |
| {belopp} var | {amount} each |
| Alla betalar lika mycket | Everyone pays the same |
| {n} % av 100 % fördelat | {n}% of 100% allocated |
| {belopp} av {belopp} fördelat | {amount} of {amount} allocated |
| Upprepas | Repeats |
| Bokförs först | First posts |
| Slutar (Aldrig / Datum / Efter antal) / gånger | Ends (Never / Date / After N) / times (recurring-end-date) |
| Settl bokför den i loggboken varje period och delar om automatiskt. Ställ in en gång, glöm den sen. | Recurring form explainer |
| Lägg i loggboken / Sätt på repeat / Spara ändringar | Add to the ledger / Put on repeat / Save changes |
| Ange ett belopp först | Enter an amount first (toast) |
| Procenten måste bli 100 | The percentages must total 100 (toast) |
| Delningen måste bli {belopp} | The split must total {amount} (toast) |
| Lägg till någon att dela med | Add someone to split with (toast) |
| Välj ett slutdatum / Slutdatumet kan inte vara före första bokföringen / Ange hur många gånger den ska bokföras | End-date validation toasts |
| Tillagd i loggboken / På repeat — bokförs först {datum} / Ändrad | Success toasts (expense / recurring / edit) |
| Utan titel | Untitled (default blank title) |

### Entry detail
| sv | en |
|---|---|
| Utgift / På repeat | Expense / On repeat (badge — no `Lån`, ADR-0020) |
| Delas lika / Egna procent / Egna belopp | Split equally / Own percentages / Own amounts |
| Du betalade / {namn} betalade | You paid / {name} paid |
| Bokförd automatiskt från "{titel}" | Posted automatically from "{title}" |
| · betalade / · skyldig | · paid / · owes (share-row tags) |
| Kategori: {label} — tryck för att ändra | Category: {label} — tap to change (ADR-0012) |
| Redigera / Ta bort | Edit / Delete (⋯ menu, ADR-0018) |
| Låst — öppna igen för att ändra eller ta bort. | Locked — reopen to edit or delete. |
| Posten är låst | The entry is locked |
| Markera som reglerad | Mark as settled |
| Reglerad ✓ — tryck för att öppna igen | Settled ✓ — tap to reopen |
| Betala varandra hur ni vill — markera sen som reglerad här. | Entry settle footer |
| Ta bort posten? / Det här ändrar saldot för alla som var med | Delete-entry confirm |
| Reglerad — snyggt / Öppnad igen | Settled — nice / Reopened (toasts) |

### Row sub-status
| sv | en |
|---|---|
| du är skyldig {belopp} | you owe {amount} (destructive) |
| du ska få {belopp} | you're owed {amount} (--success) |
| reglerad | settled |
| din del reglerad | your share settled |
| ingen andel | not your share |
| öppen | open |

### Settle-up
| sv | en |
|---|---|
| Gör upp med {namn} | Settle up with {name} |
| {namn} är skyldig dig | {name} owes you |
| Du är skyldig {namn} | You owe {name} |
| Allt är kvitt | Everything's square |
| Betala som ni brukar — Swish, kontanter, banköverföring. Settl håller bara boken i ordning. | Settle explainer, no Swish link |
| Betala först — bocka sedan av. Settl bokför inget automatiskt. | Settle explainer, with Swish link (ADR-0026) |
| Betala med Swish | Pay with Swish (action) |
| Skanna med Swish-appen | Scan with the Swish app (desktop QR caption) |
| Lägg till ditt nummer så kan {namn} Swisha dig med rätt belopp när ni gör upp. | Add-your-number nudge (creditor, ADR-0026) |
| Lägg till nummer | Add number (button) |
| Markera allt som reglerat | Mark everything as settled |
| Uppgjort med {namn} — rent bord | Settled with {name} — clean slate (toast) |
| Tidigare uppgörelser med {namn} / Inga tidigare uppgörelser än | Past settlements with {name} / none yet (settlement-history) |
| {Du/namn} gjorde upp · {n} post/poster | {You/name} settled · {n} entries (history row) |

### Recurring detail
| sv | en |
|---|---|
| Nästa: {titel} — {datum} | Next: {title} — {date} |
| Bokförs automatiskt {när} | Posts automatically {when} |
| Avslutad — {datum} / Inga fler bokföringar — redigera slutdatumet för att återuppta | Ended — {date} / no more postings (recurring-end-date) |
| Pausad — ingen kommande bokföring | Paused — no upcoming posting |
| Återuppta för att schemalägga nästa period | Resume to schedule the next period |
| Hur den delas | How it's split |
| Ändrad delning gäller framtida perioder. | A changed split applies to future periods. |
| Tidigare perioder | Previous periods |
| Pausa autobokföring / Återuppta autobokföring | Pause / Resume auto-posting |
| {titel} återupptagen / {titel} pausad / {titel} borttagen | Resume / pause / delete toasts |

### Profil (see profile-addendum + ADR-0026 + reminder-delivery)
| sv | en |
|---|---|
| Profil / Tillbaka | Profile / Back |
| Byt emoji / Ta bort / Välj profilbild | Change emoji / Remove / Choose avatar |
| Namn / Så här syns du i loggboken. | Name / This is how you appear in the ledger. |
| Ditt nummer (valfritt) | Your number (optional) — powers Swish, ADR-0026 |
| Andra i hushållet kan Swisha dig direkt när ni gör upp. Sparas som det är — inte verifierat. | Number helper (unverified) |
| Konto / E-post / Din inloggning | Account / Email / Your sign-in |
| Påminnelser via e-post | Email reminders (opt-in, off by default) |
| Spara / Sparar… / Logga ut | Save / Saving… / Sign out |
| Profilen sparad / Profilbild uppdaterad | Profile saved / Avatar updated (toasts) |

### Kontakter (see ADR-0019 + contacts-addendum)
| sv | en |
|---|---|
| Kontakter / Sparade vänner och familj — lägg till via telefonnummer. | Contacts / subtitle |
| Väntar på svar / SMS-inbjudan / E-postinbjudan / Väntar | Awaiting reply / SMS invite / Email invite / Pending |
| I {n} hushåll med dig / Ingen delad bok än | In {n} households with you / No shared book yet |
| + Lägg till kontakt | + Add contact |

### Statistik (see household-statistics)
| sv | en |
|---|---|
| Statistik / Vem la ut vad, månad för månad. | Statistics / Who paid what, month by month. |
| Inget att visa än. … | Empty state |
| Visar vem som lagt ut hur mycket per månad, de senaste 12 månaderna. … | Footer explainer |

### Hushåll (manage — see ADR-0016 / ADR-0022 / household-ownership)
| sv | en |
|---|---|
| Hushåll / Medlemmar / Ägare / Du | Household / Members / Owner / You |
| {n} medlem / {n} medlemmar | {n} member / {n} members |
| Överför ägarskap / Arkivera hushåll / Ta bort hushåll / Lämna hushåll | Transfer ownership / Archive / Delete / Leave |

### Date helpers
| sv | en |
|---|---|
| idag / imorgon / om {n} dagar | today / tomorrow / in {n} days |
| Idag / Igår | Today / Yesterday (ledger groups) |
| jan feb mar apr maj jun jul aug sep okt nov dec | month abbreviations, format `{d} {mån}` |

---

## 7. Formatting rules

- **Money = integer minor units (öre).** Convert to major units for display: `major = minor / 100`.
- **Amount string:** `Math.abs(major).toLocaleString('sv-SE', { minimumFractionDigits: 0, maximumFractionDigits: 2 }) + ' kr'` — sv-SE grouping (space thousands separator, comma decimal) + a **non-breaking space (U+00A0)** before `kr`. Build a single `<Money>` component / `formatKr()` helper; never inline.
- **Sign convention:** always show the **absolute value**; convey sign by **color + label**, not a minus in the number. Exception: **settle-sheet line items** prefix `+` (positive) or `−` (U+2212 minus, negative) explicitly.
- **Zero/square** normalizes to `0 kr` via the same helper (the prototype's stray regular-space `0 kr` is a bug — use the helper consistently, always non-breaking space).
- **Fonts:** all amounts and numeric inputs use **Spline Sans Mono → `font-mono`**; all other text uses **Bricolage Grotesque** (UI default). Sizes seen: hero 38px, sheet amounts 28–32px, row amounts ~13.5px, add-form amount input 38px.
- **Colors:** `--success` (custom token) for "owed to you"; `destructive` for "you owe"; muted for square. `--radius: 1rem` globally (cards 16–24px, pills 999px). Use Tailwind v4 **semantic tokens only** — do not hardcode the hex values from the handoff; do not port the three named palettes (see §3, ambiguities).
- **Dates:** relative via `inDays()` → `idag` / `imorgon` / `om {n} dagar`; ledger day groups `Idag` / `Igår` / `{d} {mån}` (months `jan…dec`). **Timestamps are UTC** (ADR + project rules); render in the user's locale. Do NOT hardwire "today" — the prototype's fixed `2026-07-12` is a fixture only; use real `now`.
- **Focus:** keep accessible focus rings via Tailwind `ring` using the accent/ring token.

---

## Ambiguities & chosen defaults

| # | Question | Chosen default | Rationale |
|---|---|---|---|
| 1 | Settled state: prototype `settled` boolean + `paidPairs` vs handoff `settledPairs` vs ADR | **First-class settlement events; settled derived, never stored** | ADR-0007 is binding on the model. |
| 2 | Split storage: live `{mode,vals}` recomputed vs frozen shares | **Store formula + frozen integer shares (deterministic remainder)** | ADR-0007. |
| 3 | Money type: prototype floats/USD `$` vs SEK | **Integer minor units (öre), SEK, sv-SE + ` kr`** | ADR-0007 + Settl App copy. |
| 4 | Recurring model: entries with `recId` conflated with templates | **Template entity + posted entries linking back via `recurringId`** | ADR-0007. |
| 5 | Nudge thresholds: prototype USD ($150/$75) | **1500 kr expense, 750 kr balance, recurring ≤5 days** | ADR-0007 / handoff. |
| 6 | Balance nudge: ADR "crossing 750 kr" (event) vs prototype simple `>= 750` | **Resolved (ADR-0023): fires on crossing, detected by replaying the pair's net over `Entry.CreatedAt` / `Settlement.SettledAt`; shown only if the most recent crossing is within 7 days. No storage.** | The prior state is already in the event log — crossing needs no new tables, so this upholds ADR-0007's "derived on read" instead of amending it. |
| 7 | Overlays: prototype `state.sheet` vs deep-linkable | **URL search params (`?sheet=…&id=…`) over the active route** | Deep-linkable, back-button friendly, matches TanStack Router idioms; Settl Web already implies these URLs. |
| 8 | Themes: six named palettes (Mint/Papper/Citrus × light/dark) | **Ship one token set with real light+dark only; named directions are prototype/harness-only** | Task + handoff: real light+dark, `--success` custom token; named directions are look demos. |
| 9 | Theme/dark-mode toggle placement (not in canonical app UI) | **Sidebar footer (desktop) / settings area in household drawer (mobile); default follow system + persist** | No canonical placement; keep it out of the main content chrome. |
| 10 | Dev user switcher placement (absent from all exports; user always "You") | **Dev-only (`import.meta.env.DEV`) control in sidebar footer / bottom of household drawer; not in production** | Exports have no user switching; auth is Identity (ADR-0005). Purely a dev aid. |
| 11 | Empty states: Hushållet log, `Saldon`, Recurring, Statistik, Kontakter have none coded | **Add centered muted empty states** (Hushållet per-filter `Inga poster än` / `Inga utgifter än` / `Inga repeat-poster än`, Recurring `Inget på repeat än`, etc.) | Prototype relied on always-present seed data; production needs empties. **Resolved** — coded in the built app (§2). |
| 12 | Error states: none in exports | **Inline retry card per screen + toast on mutation failure** | Standard resilience; not specified by design. |
| 13 | Upcoming ghost cards: handoff says "not clickable", prototype makes the mobile ones clickable → Recurring detail | **Clickable → Recurring detail (follow prototype)**; keep dashed ghost styling | Prototype behavior is authoritative for interactions; the detail-open affordance is a deliberate convenience. (Now on the Hushållet `På gång` rail.) |
| 14 | recShare monthly math: prototype multiplies biweekly `×2` but omits weekly `×4` (inconsistent vs recTotal `×4`) | **API normalizes consistently: monthly ×1, biweekly ×2, weekly ×4 for both recTotal and recShare** | Prototype inconsistency flagged in inventory; API owns the math (ADR-0006). |
| 15 | Settle explainer payment methods: prototype "Venmo" | **"Swish, kontanter, banköverföring" (Settl App copy)** | Settl App is canonical for copy; Venmo is US-specific. |
| 16 | Type/tab label variants: `Återkommande` (add) vs `Repeat` (ledger filter, mobile tab) vs `På repeat` (desktop nav/title) | **Keep all three deliberate variants as-is** | Confirmed intentional across Settl App/Web exports. |
| 17 | Household switcher on desktop: `DropdownMenu` vs `Drawer` | **Open the same "Dina hushåll" responsive sheet from the sidebar button** | Simpler single implementation; handoff allows either. |
| 18 | Reminder tone default: JSON prop `direct` vs renderVals fallback `gentle` | **Fixed `direct`** (matches Settl App/Web harness prop default) | Web harness passes `reminder-tone: "direct"`. The per-user tone chooser on `/profil` was removed — tone is fixed to the direct voice. The Profile screen now only exposes the nudge-email opt-in switch (off by default, reminder-delivery + ADR-0024). |

**Post-synthesis reconciliations (ADR-governed; documented here for the audit trail):**

| # | Item | Resolution | Governed by |
|---|---|---|---|
| 19 | IOU / `Lån` entry type (a third add-entry type, `⇄` glyph, `Lån` ledger filter, "informal loan" note) | **Removed.** Every entry is an `expense` or `recurringPost`; "one owes all" is the `Allt` split preset (payer share = 0). No `Lån` tab, filter, glyph, or badge anywhere. | **ADR-0020** |
| 20 | Home IA: single-book dashboard at `/` vs multi-household overview | **`/` is always the Overview; a merged `Hushållet` book (`/hushallet`, `/hushall/$id`) is the per-household dashboard + ledger.** The standalone `/ledger` tab is retired (redirects). | **ADR-0021** + home-hushallet-addendum |
| 21 | 4th nav tab + Notiser placement | **`Statistik` takes the 4th tab slot; Notiser (`/activity`) has no tab** — mobile header bell + desktop right rail. | household-statistics spec |
| 22 | Recurring lifetime | **Optional `EndDate` (`Slutar`: Aldrig / Datum / Efter antal); `Ended` derived; ended templates read `Avslutad`.** | recurring-end-date spec |
| 23 | Entry categories | **Server-assigned `Category` (12 groups + `Other`) from the title; drives the expense row icon; user can change it on entry detail.** | **ADR-0012** |
| 24 | Member phone number | **One `PhoneNumber` on `/profil` ("Ditt nummer"), powers Swish; the Contacts own-number input is gone.** | **ADR-0026** |
| 25 | Nudge out-of-app delivery | **Daily email digest via Resend, opt-in off by default (profile switch); one-click unsubscribe; de-duplicated emitted-nudge log.** | **ADR-0024** + reminder-delivery spec |
| 26 | Settle payment + history | **`Betala med Swish` action (debtor, SEK, creditor has a number) — deep link on mobile / QR on desktop; a `Tidigare uppgörelser` history list.** | swish-settlement-payments + settlement-history specs |
