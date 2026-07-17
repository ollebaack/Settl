# Settl implementation map

Synthesis of the design exports in `docs/design/` into one build spec for `apps/web`.

**Source precedence (binding):**

- **Model** disagreements → **ADR-0007** wins (`docs/adr/0007-ledger-data-model.md`).
- **UI structure / interactions** → **`Settl Prototype.dc.html`** is authoritative (original English prototype; behavior canonical, copy/currency NOT).
- **UI copy / Swedish wording / SEK / formatting** → **`Settl App`** wins (the canonical Swedish app, embedded and rendered by `Settl Web.dc.html`).
- `Settl Web.dc.html` is a **responsive harness** (renders `Settl App` in a desktop + mobile frame). It is NOT a screen — build the app once, responsively. `support.js` is the generic dc-runtime and contains zero app logic.
- `Settl Shadcn Handoff.dc.html` gives the theme tokens, component mapping, and behavioral must-haves.
- The `test` inventory is empty and contributes nothing.

**Stack:** React 19 + Vite, TanStack Router (file-based routes in `src/routes/`) + TanStack Query, Tailwind v4 semantic tokens only, shadcn (Base UI / Luma). Business logic lives in the API (ADR-0006) — the SPA renders and calls, never computes balances, shares, nudges, or settled state.

**Current repo state:** routes `__root.tsx` + `index.tsx` exist; only `button` and `card` are installed under `src/components/ui/`. The API exposes only `/health` today — every API field/query named below is a **proposed contract** to be built from ADR-0007 and regenerated into `packages/api-client` in the same PR that consumes it.

---

## 1. Route table

The core screens are TanStack file-based routes. The five overlays are the **same bottom-sheet pattern** (Drawer on mobile / Dialog on desktop) driven by URL **search params** on whatever route is active, so they are deep-linkable and the underlying screen stays mounted. Only one overlay open at a time. Screens added after this synthesis (`/profil`, `/kontakter`, `/statistik`) are specified in their own ADRs/addenda; the rows for the two that hold the member's number are included below for reference.

| Route | Screen (sv / en) | Purpose | Defined by |
|---|---|---|---|
| `/` | **Hem** / Home | Multi-household overview: roll-up hero, per-book nets, add-number banner (ADR-0026) | ADR-0021 + multi-household-overview-addendum (supersedes the single-book "Home" below) |
| `/ledger` | **Loggboken** / Ledger | Full chronological ledger, type filter, day groups | Settl App; Prototype "Ledger" |
| `/recurring` | **På repeat** / Recurring | Recurring templates, monthly totals, cycle progress, pause/resume | Settl App; Prototype "Repeats" |
| `/activity` | **Notiser** / Activity (Nudges) | Event-driven nudges (3 triggers) | Settl App; Prototype "Activity" |
| `/profil` | **Profil** / Profile | Name, avatar emoji, nudge prefs, and the member's **single number** ("Ditt nummer" — powers Swish) | profile-addendum + **ADR-0026** |
| `/kontakter` | **Kontakter** / Contacts | Saved contacts + blind SMS invites; **no own-number input** (moved to Profil, ADR-0026) | ADR-0019 + contacts-addendum |
| `/?sheet=households` | **Dina hushåll** / Household switcher | Switch household (many-to-many) | overlay; Prototype `sheet.kind='hh'` |
| `/?sheet=add` | **Ny post** / Add entry | Create expense / IOU / recurring | overlay; `sheet.kind='add'` |
| `/?sheet=entry&id=…` | Entry detail | Inspect entry, per-person shares, settle/reopen | overlay; `sheet.kind='entry'` |
| `/?sheet=settle&person=…` | **Gör upp med {namn}** / Settle-up | Net with one person, contributing entries, settle all | overlay; `sheet.kind='settle'` |
| `/?sheet=recurring&id=…` | Recurring detail | Next post, split, past cycles, pause/resume | overlay; `sheet.kind='rec'` |

**Responsive shell (not a route):** `wide = innerWidth >= 980`. Desktop = 3-column grid `220px minmax(0,1fr) 300px`, max-width 1240px, center feed capped 640px. Mobile = single column, root `max-w-md`, sticky header + bottom tab bar (`padding-bottom` clears the bar). Overlays: centered **Dialog** on desktop, bottom **Drawer** (vaul) on mobile — one responsive wrapper component.

---

## 2. Per-screen spec

Shared row conventions used by Home "Senaste" and all Ledger rows (build once as `<EntryRow>`):

- **Glyph tile** (36px rounded): IOU `⇄` (U+21C4, chip bg); recurring `↻` (U+21BB, soft/accent bg); expense = first uppercased letter of title (chip bg).
- **Meta line:** `Du betalade` / `{namn} betalade`; recurring `Bokförd automatiskt · {du betalar|namn betalar} · {datum}`; IOU `Lån till {namn}` / `Lån från {namn}` (English gloss: "you paid / name paid"; "posted automatically…"; "IOU to/from name").
- **Right column:** mono amount + colored sub-status. Sub-status precedence (all **derived from API**, never a stored flag): fully closed → `reglerad` at `opacity .5`; else you owe → `du är skyldig {belopp}` (destructive); else owed → `du ska få {belopp}` (`--success`); else some pair settled → `din del reglerad`; else `ingen andel` (no share). ("settled / you owe / you're owed / your share settled / not your share").
- Whole row is a **button** → Card + Avatar, opens Entry detail.

### 2.1 Hem `/`

> **Superseded:** the live `/` is the multi-household **Overview** (ADR-0021 + multi-household-overview-addendum), not the single-book home specced below. It also carries the dismissible **"Lägg till ditt nummer"** banner (ADR-0026) — shown when you're owed in ≥1 book and have no number saved; button → `/profil`, dismissal persisted in `localStorage` so it never nags. The single-book layout below is kept for the per-book dashboard heritage.

**Components**
- **Net hero** — `Card`; label uppercase, 38px mono amount, muted sub. Amount color: `--success` when owed to you, `destructive` when you owe, muted when square.
- **Per-person balance rows** — `Card` + `Avatar` (38px, member bg, initial); whole row a button → Settle-up sheet for that person. Right amount mono.
- **"På gång" upcoming rail (MOBILE ONLY)** — horizontally scrollable dashed ghost `Card`s (1.5px dashed, min-w 150px). Shown only when `upcomingPreview && upcoming.length>0 && !wide`. On **desktop this content moves to the right rail**. Prototype: these ghost cards ARE clickable → Recurring detail (overriding the handoff "not clickable" note — see ambiguities).
- **"Senaste" recent list** — top 4 entries by date desc, `<EntryRow>`; header has **"Visa alla"** ghost link → `/ledger`.

**Data (proposed API)** — `GET /households/{id}/summary` returning: `overallNet` (signed minor units), `netLabel` enum, `openCount`; `people[]` = `{ memberId, name, initial, avatarColor, net, relation }`; `upcoming[]` = `{ recurringId, title, nextPostDate, daysUntil, yourShare, amount }`. Recent via `GET /households/{id}/entries?limit=4&sort=date_desc`. All derived server-side.

**Copy** (sv → en):
- Net label: `Allt är kvitt` ("everything's square"), `Du ska få totalt` ("you're owed in total"), `Du är skyldig totalt` ("you owe in total").
- Net sub: `{n} öppna poster i {hushåll}` ("{n} open entries in {household}").
- Person relation: `är skyldig dig` / `du är skyldig` / `kvitt` ("owes you / you owe / square").
- `På gång` ("upcoming"); upcoming when-line `Bokförs {datum} · {när}` ("posts on {date} · {when}"); `din del` ("your share").
- `Senaste` ("latest"); `Visa alla` ("show all").

**States** — Loading: skeleton hero + 2–3 row skeletons. Empty balances (all square): net shows `0 kr`, each person amount `—` in muted. Empty recent (**no coded empty state in exports** → add one): centered muted "Inga poster än" (see ambiguities). Empty upcoming → rail hidden. Error: inline retry card. Settled rows render at `opacity .5`.

### 2.2 Loggboken `/ledger`

**Components**
- Title **"Loggboken"** (h2, 19px bold).
- **Filter pills** — `ToggleGroup` single-select (or `Badge` pills): `Alla / Utgifter / Lån / Repeat` → `filter` = `all|expense|iou|recurring`. Active = foreground bg / background text; inactive = chip bg / muted text.
- **Upcoming dashed rows (MOBILE, conditional)** — dashed ghost `Card` rows; shown when `upcomingPreview && upcoming>0 && filter ∉ {iou,expense} && !wide`. `↻` glyph, title, `Bokförs…` accent line, muted amount → Recurring detail.
- **Day-grouped entry rows** — group header (`Idag` / `Igår` / `{d} {mån}`) + `<EntryRow>` buttons.

**Data** — `GET /households/{id}/entries?type={filter}&sort=date_desc`; group client-side by `daysUntil(date)` (0→`Idag`, −1→`Igår`, else `{d} {mån}`). Upcoming from summary.

**Copy** — `Loggboken` ("the ledger"); filters `Alla / Utgifter / Lån / Repeat` ("All / Expenses / Loans (IOUs) / Repeat"); `Idag` / `Igår` ("today / yesterday"). Note deliberate label variants: Add-sheet type is `Återkommande`, ledger filter + mobile tab is `Repeat`, desktop nav is `På repeat`.

**States** — Loading: grouped row skeletons. Empty filter result: **no coded empty state** → add centered muted per filter (see ambiguities). Filter `Lån`/`Utgifter` hides upcoming block. Error: retry card.

### 2.3 På repeat `/recurring`

**Components**
- Title **"På repeat"** + subtitle.
- **Two summary tiles** — two `Card`s (right uses `--accent`/soft bg + accent text): left `delat / månad` (recTotal), right `din del / månad` (recShare). Both mono.
- **Recurring template cards** — `Card` + `Progress`. `↻` icon, title, `{kadens} · {du betalar|namn betalar}` sub, amount + `/ mån` or `/ 2 v`. **Progress = elapsed share of current cycle** = `clamp(1 − daysLeft/cycleLen, 0.04, 1)` (cycleLen: monthly 30 / biweekly 14 / weekly 7). Footer: next-post accent label (`Bokförs {när}` / `Pausad`), overlapping member avatars, split label. Two buttons: **"Detaljer"** (chip) → Recurring detail; **"Pausa" / "Återuppta"** (outline) toggles `active`. Paused card `opacity .55`, progress `0%`.
- **Footer explainer** (muted caption).

**Data** — `GET /households/{id}/recurring` → templates (active + paused) with server-computed `monthlyNormalized`, `yourShare`, `cycleProgress`, `nextPostDate`, `daysUntil`, `payerName`, `splitMode`, `contributingAvatars[]`, `active`. `recTotal` = Σ monthly-normalized active; `recShare` = your monthly-normalized share. (Prototype's monthly math — `×2` biweekly, `×4` weekly, and the recShare `×2`-only inconsistency — moves to the API and must be made consistent there.)

**Copy** — subtitle `Återkommande kostnader bokför sig själva — ingen månatlig inmatning.` ("Recurring costs post themselves — no monthly data entry."); tiles `delat / månad`, `din del / månad`; cadence `Varje månad / Varannan vecka / Varje vecka`; units `/ mån`, `/ 2 v`; split labels `Delas lika / Egna procent / Egna belopp` ("split equally / own percentages / own amounts"); `Detaljer`; `Pausa` / `Återuppta`; next-post `Bokförs {när}` / `Pausad`; footer `Varje period bokför Settl posten i loggboken och delar om den — ändra delningen när som helst, det gäller framtida perioder.`

**States** — Loading: tile + 2 card skeletons. Active vs paused (dimmed, 0% bar, `Pausad`, `Återuppta` button). Pause/resume fires toast + refetch. Empty (**no coded empty state**) → add "Inget på repeat än" (see ambiguities). Error: retry card.

### 2.4 Notiser `/activity`

**Components**
- Title **"Notiser"** + subtitle `Settl säger bara till när något händer.`
- **Nudge cards** — `Card` + secondary `Button`(s). Accent dot, title, `when` timestamp (right, muted), body (tone-dependent), 1–2 action pills `Visa` / `Gör upp`. `Visa` → related entry/recurring detail; `Gör upp` → Settle-up sheet for the payer (shown only when payer ≠ you).
- **Empty state** — centered bell icon + `Allt lugnt` + calm one-liner (no trigger listing; that lives in the triggers card).
- **Triggers card** (always shown) — eyebrow `Du hör av oss när` over a 3-row icon list of the event triggers. Replaces the old redundant footer sentence with a scannable reference.

**Data** — `GET /households/{id}/nudges?tone={gentle|direct}` → derived on read (ADR-0007, no storage) from 3 triggers, **in this order**: (1) active recurring `daysUntil(next) <= 5`; (2) non-IOU entry, unsettled, `amount >= 1500 kr` within last 7 days; (3) pairwise net `abs >= 750 kr`. Each nudge: `{ title, body, when, actions[] }` with copy pre-selected by tone. Trigger (3) fires on **crossing**, not while-over: the API replays the pair's net over `Entry.CreatedAt` / `Settlement.SettledAt` and emits only if the most recent upward crossing of 750 kr is within 7 days — no storage (ADR-0023, resolves ambiguity #6).

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

**Components** — responsive sheet (Drawer mobile / Dialog desktop) titled **"Dina hushåll"** + subtitle `En bok per hushåll — du kan vara med i flera.`. Household rows = `Card` buttons: initial square, name, member names comma-joined, right net amount + sub (`du ska få` / `du är skyldig` / `kvitt`); active household outlined in accent. Bottom **"+ Nytt hushåll"** dashed outline `Button` opens the create-household sheet below.

**Data** — `GET /households` → `{ id, name, memberNames[], net, active }`.

**Copy** — `Dina hushåll` ("Your households"); `En bok per hushåll — du kan vara med i flera.` ("One book per household — you can be in several."); row sub `du ska få / du är skyldig / kvitt`; `+ Nytt hushåll`.

**States** — Selecting a household: switches the active household, reset to `/` + filter `all`, close sheet, toast `Bytte till {namn}`. Loading: row skeletons.

### 2.5b Nytt hushåll (overlay `?sheet=newHousehold`)

**Components** — responsive sheet titled **"Nytt hushåll"** + subtitle `En egen bok för stället eller gänget — bjud in vilka som helst.`. Name `Input`. "Medlemmar" card: fixed row for the acting user (`Du`, "alltid med"), then one row per typed member name with a remove `×` button, then **"+ Lägg till medlem"**. Helper text `I riktiga appen får medlemmarna en inbjudan — här läggs de till direkt.` (no invite flow yet — deferred, tech-debt 0001/0003). Save button **"Skapa hushåll"**, disabled until the name is non-empty.

**Data** — `POST /households` `CreateHousehold` `{ name, newMemberNames: string[] }` (see §7); the acting user is always included server-side. → 201 household.

**Copy** — `Nytt hushåll`; `En egen bok för stället eller gänget — bjud in vilka som helst.`; `Medlemmar`; `alltid med`; `+ Lägg till medlem`; `I riktiga appen får medlemmarna en inbjudan — här läggs de till direkt.`; `Skapa hushåll`; validation toast `Ge hushållet ett namn först`; success toast `{namn} skapat — tomt blad`.

**States** — On save: create, switch active household to the new one, reset to `/` + filter `all`, close sheet, toast. Blank member name rows are dropped silently on save.

### 2.6 Ny post (overlay `?sheet=add`)

**Components**
- Title **"Ny post"**.
- **Type picker** — `Tabs` (pill): `Utgift / Lån / Återkommande`.
- **Amount input** — `Input` mono borderless, 38px right-aligned, placeholder `0,00`, `inputMode="decimal"`, accepts comma decimal; `kr` suffix.
- **Title input** — placeholder by type: expense `Mat, middag, biljetter…`, IOU `Vad gäller det?`, recurring `Hyra, internet, Spotify…`.
- **IOU branch (Lån)** — `ToggleGroup` single `Åt vilket håll`: `Jag är skyldig` (iowe) / `Skyldig mig` (theyowe); + `ToggleGroup` `Med` (other members).
- **Expense/Recurring branch** — `ToggleGroup` `Vem betalade` (payers) + `ToggleGroup` `Delning`: `Lika / % / kr` + a `Card` of per-member share rows. Equal → read-only equal share text; percent/amount → numeric `Input` per member with unit (`%` / `kr`). **Live balance hint** below rows, color-coded (`--success` when balanced, destructive when off).
- **Recurring branch** — `ToggleGroup` `Upprepas`: `Varje månad / Varannan vecka / Varje vecka` + date `Input` `Bokförs först` + explainer.
- **Save button** — label by type; `opacity .45` until amount > 0 (still clickable → validation toast).

**Data** — form: `{ type, title, amount, paidBy, iouDir, iouWith, splitMode, vals{}, cadence, date }`; members from active household. Save → `POST /households/{id}/entries` (expense/iou) or `POST /households/{id}/recurring`. **API freezes integer shares** with deterministic remainder distribution (ADR-0007); amount is **integer minor units (öre)**.

**Copy** — `Ny post`; types `Utgift / Lån / Återkommande` ("Expense / Loan / Recurring"); `0,00`; placeholders above; `Åt vilket håll` ("which direction"), `Jag är skyldig` / `Skyldig mig` ("I owe / owed to me"), `Med` ("with"); `Vem betalade` ("who paid"); `Delning` ("split"), `Lika / % / kr`; equal hints `{belopp} var` ("{amount} each") / `Alla betalar lika mycket` ("everyone pays the same"); percent hint `{n} % av 100 % fördelat`; amount hint `{belopp} av {belopp} fördelat`; `Upprepas` ("repeats"); `Bokförs först` ("first posts"); recurring explainer `Settl bokför den i loggboken varje period och delar om automatiskt. Ställ in en gång, glöm den sen.`; save labels `Lägg i loggboken` / `Anteckna` / `Sätt på repeat`.

**States / validation** (on save, surfaced as **toasts**, not inline field errors; keep the inline color-coded hint too):
- amount ≤ 0 → `Ange ett belopp först`.
- percent sum ≠ 100 (±0.5) → `Procenten måste bli 100`.
- amount split sum ≠ total (±0.05 kr) → `Delningen måste bli {belopp}`.
- Success (differ by type): `Tillagd i loggboken` / `Antecknat` / `På repeat — bokförs först {datum}`. On success sheet closes, form resets, list prepends; recurring jumps to `/recurring`.
- Default titles when blank: IOU → `Lån`, other → `Utan titel`.
- Loading (submit): disable button, spinner. Error (API): toast + keep form.

### 2.7 Entry detail (overlay `?sheet=entry&id=`)

**Components** — `Badge` type (`Utgift` / `Lån` / `På repeat`) + date; title, amount (mono 28px), meta (`Du betalade`/`{namn} betalade` + split label, or `Informellt lån — inget kvitto behövs` for IOU); optional accent auto-post note `Bokförd automatiskt från ”{titel}”`; split share rows (`Card` + `Avatar`, name + tag, mono share); toggle settle `Button` (primary when open, chip when settled); footer caption.

**Data** — `GET /entries/{id}` → `{ type, date, title, amount, paidBy, splitMode, shares[], recurringId, templateTitle }` with **frozen shares** and **derived** settled state (from settlement events). Settle → `POST /entries/{id}/settlements`; reopen → `DELETE` the settlement (ADR-0007: locked once a settlement touches it, reopen first).

**Copy** — badges `Utgift / Lån / På repeat`; `Informellt lån — inget kvitto behövs` ("informal loan — no receipt needed"); `Du betalade` / `{namn} betalade`; `Bokförd automatiskt från ”{titel}”`; row tags `· betalade` / `· skyldig` / `· ska få` ("paid / owes / to receive"); button `Markera som reglerad` ("mark as settled") / `Reglerad ✓ — tryck för att öppna igen` ("settled ✓ — tap to reopen"); footer `Betala varandra hur ni vill — markera sen som reglerad här.`; toasts `Reglerad — snyggt` / `Öppnad igen`.

**States** — Loading: skeleton. Settled vs open swaps button style/label. Error: retry.

### 2.8 Gör upp (overlay `?sheet=settle&person=`)

**Components** — title **"Gör upp med {namn}"**; net summary (label + mono 32px colored amount); contributing entry list (`Card` rows: title, date, **signed** amount — `+` positive / `−` U+2212 minus negative, colored); explainer; **"Betala med Swish"** action when the API returns `swishPay` (debtor, SEK, creditor has a number) — a tap-through deep link on mobile, a QR of the same `uri` on desktop (swish-settlement-payments spec); **"add your number" nudge** shown to the creditor (`owesYou`) who has no number saved — an accent `Card` with a `Lägg till nummer` button → `/profil` (ADR-0026); **"Markera allt som reglerat"** primary `Button`.

**Data** — `GET /households/{id}/settle-preview?person={memberId}` → `{ net, netLabel, memberName, entries[]{ id, title, date, signedAmount }, swishPay?{ uri, amountMinor } }` (`swishPay` non-null only for the debtor, SEK, when the creditor saved a number — the URL is built server-side, ADR-0006). The creditor-side nudge is derived client-side from `netLabel === 'owesYou'` + the current member's empty number. Confirm → `POST /households/{id}/settlements` with the pair → records **one first-class settlement event** closing all open pair debts; entries with no remaining open debt become settled (derived).

**Copy** — `Gör upp med {namn}`; net label `{namn} är skyldig dig` / `Du är skyldig {namn}` / `Allt är kvitt`; explainer has two variants — with `swishPay`: `Betala först — bocka sedan av. Settl bokför inget automatiskt.`; otherwise `Betala som ni brukar — Swish, kontanter, banköverföring. Settl håller bara boken i ordning.` (note: **Swish**, not Venmo — SEK context); Swish action `Betala med Swish` + desktop caption `Skanna med Swish-appen`; add-number nudge `Lägg till ditt nummer så kan {namn} Swisha dig med rätt belopp när ni gör upp.` + button `Lägg till nummer` (ADR-0026); primary button `Markera allt som reglerat` ("mark everything as settled"); toast `Uppgjort med {namn} — rent bord` ("settled with {name} — clean slate").

**States** — Loading: skeleton. All square → `Allt är kvitt`, empty list, button hidden/disabled. Error: retry.

### 2.9 Recurring detail (overlay `?sheet=recurring&id=`)

**Components** — `Badge` `På repeat` + cadence label; title + amount (mono 28px); **next-post dashed `Card`**: `↻`, `Nästa: {titel} — {datum}` + `Bokförs automatiskt {när}` + your share + `din del` (paused variants below); **"Hur den delas"** split rows (`Card` + `Avatar`, tag `· betalar`) + caption; **"Tidigare perioder"** posted-entry list (title, status `reglerad`/`öppen`, mono amount) — shown only if posts exist; pause/resume outline `Button`.

**Data** — `GET /recurring/{id}` → template `{ title, amount, cadence, nextPostDate, payer, splitMode, shares[], yourShare, active }` + `postedEntries[]` (linked via `recurringId`: `{ id, title, amount, settled }`). Pause/resume → `PATCH /recurring/{id} { active }`.

**Copy** — badge `På repeat`; next-post `Nästa: {titel} — {datum}` + `Bokförs automatiskt {när}`; paused `Pausad — ingen kommande bokföring` + `Återuppta för att schemalägga nästa period`; `Hur den delas` ("how it's split") + caption `Ändrad delning gäller framtida perioder.` ("a changed split applies to future periods"); `Tidigare perioder` ("previous periods") with statuses `reglerad` / `öppen`; button `Pausa autobokföring` / `Återuppta autobokföring`; toasts `{titel} återupptagen` / `{titel} pausad` / `{titel} pausad — inga fler autobokföringar` (card variant) / `{titel} resumed`→`{titel} återupptagen`.

**States** — Loading: skeleton. Paused shows paused next-post variants. No posts → hide "Tidigare perioder". Error: retry.

---

## 3. Shared shell

Single responsive `<AppShell>` around the router `<Outlet>`. `wide = innerWidth >= 980`.

**Navigation — custom component (no stock shadcn):**
- **Desktop = left sidebar** (sticky, 220px): `Settl` wordmark + tagline `Hushållets delade anteckningsbok`; household switcher button; vertical nav with active-dot — `Hem / Loggbok / På repeat / Aktivitet`; primary **"+ Ny post"** button (opens Add sheet); helper text `Interaktiv prototyp — lägg till en post, gör upp, pausa en återkommande kostnad eller byt hushåll.`.
- **Mobile = bottom tab bar** (fixed, 5-slot grid `1fr 1fr 66px 1fr 1fr`): `Hem`, `Loggbok`, center raised circular **"+" FAB** (accent, 52px, translateY −12px, accent shadow → Add sheet), `Repeat`, `Aktivitet`. Active tab = dot + foreground color; inactive = muted. Screen order fixed: Home, Ledger, (add), Repeats, Activity.
- **Mobile top header** (sticky): household switcher **pill** (initial badge + name + `▾`) on the left, overlapping member `Avatar` stack on the right (decorative).

**Household switcher** — mapped to `DropdownMenu` on desktop (or reuse the Drawer) / `Drawer` on mobile per handoff; here it opens the **Dina hushåll** sheet (§2.5). Trigger = sidebar button (desktop) / header pill (mobile). Keep multi-household (many-to-many; "You" belongs to several).

**Theme / dark-mode toggle** — the six named palettes (Mint/Papper/Citrus × light/dark) are **prototype/harness controls only**; ship real light+dark via a single `--success`-augmented token set (see §7 and handoff globals.css). Place a **light/dark toggle** in the sidebar footer (desktop) and inside the household/settings drawer (mobile). Do **not** port the three named directions as user-facing options in v1 (see ambiguities). Default: follow system, persist choice.

**Dev user switcher** — not present in any export (user is always `u1 "You"`; auth is ASP.NET Identity per ADR-0005). Add a **dev-only** user switcher (gated behind `import.meta.env.DEV`) in the sidebar footer next to the theme toggle (desktop) / bottom of the household drawer (mobile), so a developer can impersonate a seed member. Not shipped in production (see ambiguities).

**Overlays** — one `<ResponsiveSheet>` wrapper: `Dialog` (desktop, centered, radius 24px, width `min(480px,100%)`, scrim) / `Drawer`/vaul (mobile, top radius 26px, slide-up, 36×4px grip handle). Driven by the `sheet` search param; only one at a time; scrim/handle close.

**Toasts** — `Sonner`, single toast, auto-dismiss ~2600ms, bottom-center (offset ~28px desktop / ~104px mobile to clear the tab bar).

---

## 4. Flows

### Add entry (3-mode split editor + live validation)
1. Sidebar "+ Ny post" / mobile "+" FAB → Add sheet.
2. `Tabs` default **Utgift**. Amount (comma decimal) → Save un-dims when > 0. Title.
3. Expense/Recurring: pick payer (`Vem betalade`), then `Delning` mode:
   - **Lika (equal):** read-only per-member share text; hint `{belopp} var` (or `Alla betalar lika mycket` when amount empty).
   - **% (percent):** numeric `%` input per member; hint `{n} % av 100 % fördelat`, green when sum = 100 (±0.5).
   - **kr (amount):** numeric `kr` input per member; hint `{belopp} av {belopp} fördelat`, green when sum = total (±0.05 kr).
4. Recurring adds `Upprepas` cadence + `Bokförs först` date.
5. IOU branch: no split; `Åt vilket håll` direction + `Med` person.
6. Save validates → toast on failure (`Ange ett belopp först` / `Procenten måste bli 100` / `Delningen måste bli {belopp}`); on success API **freezes integer shares**, entry prepends, sheet closes, success toast, recurring jumps to `/recurring`.

**Validation rules:** percent tolerance ±0.5; amount tolerance ±0.05 kr; amount must be > 0. Inline hint is live + color-coded; hard errors are toasts. All final validation authoritative in the API (ADR-0006).

### Settle up ("Markera allt som reglerat" + per-pair)
- Home person row or nudge `Gör upp` → Settle-up sheet (§2.8). Shows net + signed contributing entries.
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

Already present: `button`, `card`.

Add:
- `drawer` (vaul) — mobile sheets
- `dialog` — desktop sheets
- `tabs` — Add-sheet type picker (pill)
- `toggle-group` — split mode, payer, IOU direction, cadence, IOU person, ledger filter
- `avatar` — member avatars / overlapping stack
- `progress` — recurring cycle progress
- `badge` — entry type badges, filter chips (alt)
- `dropdown-menu` — desktop household switcher
- `sonner` — toasts
- `input` — amount, title, per-member split, date
- `label` — form field labels
- `scroll-area` — content / upcoming rail
- `separator` — sidebar / sheet dividers
- `skeleton` — loading states (added for the empty/loading spec above)

Custom (not shadcn primitives, build in `src/components/`): `AppShell` (sidebar + tab bar + FAB), `ResponsiveSheet` (Dialog/Drawer wrapper), `EntryRow`, `GhostCard` (dashed upcoming), `Money` (mono sv-SE formatter), `ThemeToggle`, dev `UserSwitcher`.

---

## 6. Copy glossary (sv → en)

### Navigation & shell
| sv | en |
|---|---|
| Settl | Settl (brand) |
| Hushållets delade anteckningsbok | The household's shared notebook |
| Hem | Home |
| Loggbok / Loggboken | Ledger / The ledger |
| På repeat | On repeat (desktop nav / tab title) |
| Repeat | Repeat (mobile tab / ledger filter) |
| Aktivitet | Activity |
| Notiser | Nudges (screen title) |
| + Ny post | + New entry |
| Interaktiv prototyp — lägg till en post, gör upp, pausa en återkommande kostnad eller byt hushåll. | Interactive prototype helper (sidebar) |

### Home
| sv | en |
|---|---|
| Allt är kvitt | Everything's square |
| Du ska få totalt | You're owed in total |
| Du är skyldig totalt | You owe in total |
| {n} öppna poster i {hushåll} | {n} open entries in {household} |
| är skyldig dig / du är skyldig / kvitt | owes you / you owe / square |
| På gång | Upcoming |
| Bokförs {datum} · {när} | Posts on {date} · {when} |
| din del | your share |
| Senaste | Latest |
| Visa alla | Show all |
| Lägg till ditt nummer | Add your number (banner title, ADR-0026) |
| Du har pengar att få. Med ditt nummer sparat kan andra Swisha dig direkt när ni gör upp. | Add-number banner body |

### Ledger
| sv | en |
|---|---|
| Alla / Utgifter / Lån / Repeat | All / Expenses / Loans (IOUs) / Repeat |
| Idag / Igår | Today / Yesterday |

### Recurring
| sv | en |
|---|---|
| Återkommande kostnader bokför sig själva — ingen månatlig inmatning. | Recurring costs post themselves — no monthly data entry. |
| delat / månad | shared / month |
| din del / månad | your share / month |
| Varje månad / Varannan vecka / Varje vecka | Every month / Every other week / Every week |
| / mån · / 2 v | / mo · / 2 wk |
| Bokförs {när} / Pausad | Posts {when} / Paused |
| Delas lika / Egna procent / Egna belopp | Split equally / Own percentages / Own amounts |
| Detaljer | Details |
| Pausa / Återuppta | Pause / Resume |
| Varje period bokför Settl posten i loggboken och delar om den — ändra delningen när som helst, det gäller framtida perioder. | Recurring footer explainer |

### Nudges
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
| + Nytt hushåll | + New household |
| Bytte till {namn} | Switched to {name} |
| Inte i den här prototypen — än | Not in this prototype — yet |

### Add entry
| sv | en |
|---|---|
| Ny post | New entry |
| Utgift / Lån / Återkommande | Expense / Loan (IOU) / Recurring |
| 0,00 | 0.00 (amount placeholder) |
| Mat, middag, biljetter… | Groceries, dinner, tickets… (expense) |
| Vad gäller det? | What's it about? (IOU) |
| Hyra, internet, Spotify… | Rent, internet, Spotify… (recurring) |
| Åt vilket håll | Which direction |
| Jag är skyldig / Skyldig mig | I owe / Owed to me |
| Med | With |
| Vem betalade | Who paid |
| Delning | Split |
| Lika / % / kr | Equal / % / kr |
| {belopp} var | {amount} each |
| Alla betalar lika mycket | Everyone pays the same |
| {n} % av 100 % fördelat | {n}% of 100% allocated |
| {belopp} av {belopp} fördelat | {amount} of {amount} allocated |
| Upprepas | Repeats |
| Bokförs först | First posts |
| Settl bokför den i loggboken varje period och delar om automatiskt. Ställ in en gång, glöm den sen. | Recurring form explainer |
| Lägg i loggboken / Anteckna / Sätt på repeat | Add to the ledger / Note it / Put on repeat |
| Ange ett belopp först | Enter an amount first (toast) |
| Procenten måste bli 100 | The percentages must total 100 (toast) |
| Delningen måste bli {belopp} | The split must total {amount} (toast) |
| Tillagd i loggboken / Antecknat / På repeat — bokförs först {datum} | Success toasts by type |
| Lån / Utan titel | Loan / Untitled (default blank titles) |

### Entry detail
| sv | en |
|---|---|
| Utgift / Lån / På repeat | Expense / Loan / On repeat (badge) |
| Informellt lån — inget kvitto behövs | Informal loan — no receipt needed |
| Du betalade / {namn} betalade | You paid / {name} paid |
| Bokförd automatiskt från ”{titel}” | Posted automatically from ”{title}” |
| · betalade / · betalar / · skyldig / · ska få | · paid / · pays / · owes / · to receive |
| Markera som reglerad | Mark as settled |
| Reglerad ✓ — tryck för att öppna igen | Settled ✓ — tap to reopen |
| Betala varandra hur ni vill — markera sen som reglerad här. | Entry settle footer |
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

### Recurring detail
| sv | en |
|---|---|
| Nästa: {titel} — {datum} | Next: {title} — {date} |
| Bokförs automatiskt {när} | Posts automatically {when} |
| Pausad — ingen kommande bokföring | Paused — no upcoming posting |
| Återuppta för att schemalägga nästa period | Resume to schedule the next period |
| Hur den delas | How it's split |
| Ändrad delning gäller framtida perioder. | A changed split applies to future periods. |
| Tidigare perioder | Previous periods |
| Pausa autobokföring / Återuppta autobokföring | Pause / Resume auto-posting |
| {titel} återupptagen / {titel} pausad / {titel} pausad — inga fler autobokföringar | Resume/pause toasts |

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
| 11 | Empty states: Ledger, Home recent, Home people, Recurring have none coded | **Add centered muted empty states** (e.g. Ledger per-filter "Inga poster än", Recurring "Inget på repeat än") | Prototype relied on always-present seed data; production needs empties. Copy is provisional — confirm Swedish wording with design. |
| 12 | Error states: none in exports | **Inline retry card per screen + toast on mutation failure** | Standard resilience; not specified by design. |
| 13 | Upcoming ghost cards: handoff says "not clickable", prototype makes Home mobile ones clickable → Recurring detail | **Clickable → Recurring detail (follow prototype)**; keep dashed ghost styling | Prototype behavior is authoritative for interactions; the detail-open affordance is a deliberate convenience. |
| 14 | recShare monthly math: prototype multiplies biweekly `×2` but omits weekly `×4` (inconsistent vs recTotal `×4`) | **API normalizes consistently: monthly ×1, biweekly ×2, weekly ×4 for both recTotal and recShare** | Prototype inconsistency flagged in inventory; API owns the math (ADR-0006). |
| 15 | Settle explainer payment methods: prototype "Venmo" | **"Swish, kontanter, banköverföring" (Settl App copy)** | Settl App is canonical for copy; Venmo is US-specific. |
| 16 | Type/tab label variants: `Återkommande` (add) vs `Repeat` (ledger filter, mobile tab) vs `På repeat` (desktop nav/title) | **Keep all three deliberate variants as-is** | Confirmed intentional across Settl App/Web exports. |
| 17 | Household switcher on desktop: `DropdownMenu` vs `Drawer` | **Open the same "Dina hushåll" responsive sheet from the sidebar button** | Simpler single implementation; handoff allows either. |
| 18 | Reminder tone default: JSON prop `direct` vs renderVals fallback `gentle` | **Fixed `direct`** (matches Settl App/Web harness prop default) | Web harness passes `reminder-tone: "direct"`. The per-user tone chooser on `/profil` was removed — tone is fixed to the direct voice (`MeDto.nudgeTone` stays `direct`, always sent by PUT `/me`). The Profile screen now only exposes the nudge-email opt-in switch (off by default). |
