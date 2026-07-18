# Multi-household overview addendum

Extends `implementation-map.md` with the screens in **`Settl Multi-Household Overview.dc.html`**
(new export, 2026-07-16) and realizes the **[adaptive-home & multi-household overview
spec](../specs/adaptive-home-multi-household-overview.md)**. Same conventions:
UI structure from the DC export is authoritative; API fields below are **proposed
contracts**. Business logic (net, netLabel, currency roll-up, owner check) is
server-derived — the screens render and call, never compute (ADR-0006).

**Core IA decision (adaptive-home spec):** "Hem" (`/`) is **adaptive on the count of the user's
*active* households**. Zero → first-run create flow (existing). Exactly one → that
household's dashboard, *unchanged* (today's home). Two or more → the **multi-household
overview**. The single-household majority pays no tax — the overview simply does not
exist for them.

---

## 1. Route table (additions / changes)

| Route | Screen (sv / en) | Behaviour | Defined by |
|---|---|---|---|
| `/` | **Hem** / Home | **Adaptive.** ≥2 active households → Overview (frames 1–2). Exactly 1 → household dashboard (frame 3, = today's home, unchanged). 0 → create flow. | Overview frames 1–3 |
| `/` (drill-in) or `/hushall/$id` | **Hushåll** / A book | Tapping a book on the Overview sets the active household and shows that book's dashboard. Whether this is a state of `/` or a dedicated per-household route is an **implementation choice** (see §5.1) — the design fixes the screen, not the routing. | today's `index.tsx` |
| (sheet) | **Lägg till en vän** / Add a friend | Invite-by-number/email affordance opened from the Overview. | Overview frame 4 |

The existing household-switcher **sheet** (`household-switcher-sheet`) is retained as the
in-context switch reachable from Loggbok/Repeat/Aktivitet; the Overview does not replace it.

---

## 2. Per-screen spec

### 2.1 Hem — Overview, single currency (frame 1)

**When:** ≥2 active households, all sharing one currency.

**Components** — header: eyebrow `Översikt` + title `Dina hushåll`, account avatar
(circle) top-right; **roll-up hero card** (`Du ska få totalt` / `Du är skyldig totalt` /
`Allt är kvitt`, big mono net, sub `över {n} aktiva hushåll · {m} öppna poster`);
`Hushåll` section = one card per active book (household bubble = rounded square, name,
`memberNames` joined, right-aligned mono net + server `netSub` label); dashed
`+ Nytt hushåll`; `Vänner` entry card (§2.4); `Arkiverade` section (§2.5).

**Interaction** — tapping a book card sets the active household and enters it (§5.1);
`+ Nytt hushåll` opens the existing create sheet.

**Data** — `GET /households` → `HouseholdListItemDto[]` (`id, name, currency,
memberNames, netMinor, netLabel`). Hero total is summed **client-side only when every
active book shares one `currency`** (see §2.2 for the fallback). `netLabel` drives copy
and colour; `overallNetMinor`/`openCount` for the hero sub may piggyback on the
per-household summary or a small aggregate field (proposed `GET /households?include=summary`).

### 2.2 Hem — Overview, mixed currencies (frame 2)

**When:** ≥2 active households spanning ≥2 currencies.

**Difference from 2.1** — the hero shows a **descriptive roll-up, never a sum**:
`Din ställning` + `Du ska få i {x} hushåll` (pos) / `Du är skyldig i {y} hushåll` (neg),
with helper `Olika valutor — summeras inte. Öppna ett hushåll för dess saldo.` Each book
card appends its `· {CURRENCY}` to the member line and renders its net in that book's
own currency (`sv-SE` amount, currency-specific suffix — `kr`, `€`, …).

**Rule (adaptive-home spec):** the client must detect >1 distinct `currency` across active books
and switch to the descriptive hero. No cross-currency arithmetic anywhere in the UI.

### 2.3 Hem — single-household collapse (frame 3)

**When:** exactly 1 active household. `/` renders today's home dashboard verbatim (net
hero, per-person rows, `På gång`, `Senaste`). The header switcher pill remains (opens
the switcher sheet / create). Shown here only to document that the overview is absent —
**no new UI**; this is the existing `index.tsx`.

### 2.4 Lägg till en vän (frame 4) — **loose affordance**

Entry point lives on the Overview (`Vänner` card: overlapping contact circles + name +
`Bjud in via nummer eller mejl`). Opens a sheet: title `Lägg till en vän`, sub
`Skicka en inbjudan via telefonnummer eller mejl. Vi avslöjar aldrig om numret redan
finns på Settl.`; phone field (country prefix) **or** email field (divider `eller`);
primary `Skicka inbjudan`; `Dina kontakter` list (saved, reusable, each with a `Bjud in`
chip); helper `Kontakter sparas när en inbjudan accepteras och kan återanvändas i alla
hushåll.`

**Dependency — do NOT build the contact model from this design.** The contacts-by-phone
model (add-by-number = blind SMS invite, no lookup/enumeration oracle, connection-on-
accept, contacts reusable across households) is decided in a **separate contacts
workstream grilled 2026-07-16, landing on a parallel branch** (not present in this
branch — see §5.4). This addendum specifies only the *affordance and copy*; the sheet's
endpoints, the full `/kontakter` screen, and SMS delivery are that ADR's territory and
need their own design pass.

### 2.5 Arkiverade (section on frames 1–2) — ADR-0016

Archived households render as dimmed rows (`opacity ~.6`, grayscaled bubble): name,
`Arkiverad {date} · {CURRENCY}`, and a `Återställ` chip **shown to the owner only**.

**Data** — the Overview needs archived households alongside active ones. Proposed:
`GET /households?scope=archived` (or an `archived` flag + `archivedAt`, `isOwner` on the
list DTO), plus ADR-0016's `POST /households/{id}/restore`. The **adaptive threshold
counts active (non-archived) books only** — archiving the second-to-last book collapses
Hem back to the single-household dashboard.

---

## 3. Copy glossary (sv → en, additions)

| sv | en |
|---|---|
| Översikt | Overview |
| Dina hushåll | Your households |
| Du ska få totalt / Du är skyldig totalt | You're owed in total / You owe in total |
| över {n} aktiva hushåll · {m} öppna poster | across {n} active households · {m} open items |
| du ska få / du är skyldig / kvitt | you're owed / you owe / settled |
| Din ställning | Where you stand |
| Du ska få i {x} hushåll / Du är skyldig i {y} hushåll | You're owed in {x} households / You owe in {y} households |
| Olika valutor — summeras inte. Öppna ett hushåll för dess saldo. | Different currencies — not summed. Open a household for its balance. |
| Saldot per hushåll är i hushållets egen valuta. Inget kombinerat totalbelopp visas när valutorna skiljer sig. | Each household's balance is in its own currency. No combined total is shown when currencies differ. |
| Nytt hushåll | New household |
| Vänner | Friends |
| Lägg till en vän | Add a friend |
| Bjud in via nummer eller mejl | Invite by number or email |
| Skicka en inbjudan via telefonnummer eller mejl. Vi avslöjar aldrig om numret redan finns på Settl. | Send an invite by phone number or email. We never reveal whether the number is already on Settl. |
| Telefonnummer / E-post | Phone number / Email |
| Skicka inbjudan | Send invite |
| Dina kontakter | Your contacts |
| Redan i {n} hushåll | Already in {n} households |
| Bjud in | Invite |
| Kontakter sparas när en inbjudan accepteras och kan återanvändas i alla hushåll. | Contacts are saved when an invite is accepted and can be reused across all households. |
| Arkiverade | Archived |
| Arkiverad {date} | Archived {date} |
| Återställ | Restore |
| Med bara ett aktivt hushåll är Hem den vanliga instrumentpanelen — översikten dyker upp först vid två eller fler. | With only one active household, Home is the usual dashboard — the overview appears only at two or more. |

---

## 4. Desktop adaptation (not drawn — mobile-first export)

On ≥980px the Overview replaces the centre column of `<AppShell>` (frames are the
mobile presentation). The sidebar keeps the household-switcher button; the right rail
(`På gång` / `Knuffar`) is active-household-scoped and so is hidden/empty on the
Overview until a book is entered. Book cards may lay out as a 2-up grid. Flag for the
implementing PR.

---

## 5. Notes & open questions

1. **Drill-in routing.** With ≥2 books, `/` is the Overview, so the focused single-book
   dashboard needs a home distinct from the Overview. Options: (a) a dedicated
   `/hushall/$id` route reusing today's `index.tsx` layout with a back-to-overview
   affordance, or (b) a "focused" state of `/` gated on an explicit selection (active
   household alone can't disambiguate, since count is still ≥2). Recommendation: (a).
   Decide in the implementing PR.
2. **Aggregate hero fields.** `HouseholdListItemDto` covers the cards. The hero's
   `openCount` total and same-currency sum are client-derivable from the list, but a
   small server aggregate (total open count, per-currency subtotals) would keep ADR-0006
   clean. Proposed, not required.
3. **Cross-currency rule is load-bearing** — the UI must never sum across currencies;
   the descriptive roll-up (§2.2) is the only multi-currency hero.
4. **Contacts dependency.** The add-friend affordance depends on the contacts-by-phone
   decision (blind SMS invites, contact graph),
   [ADR-0019](../specs/contacts-phone-sms.md). This overview started as
   a parallel "ADR-0019" during the design build but is now the
   [adaptive-home & multi-household overview spec](../specs/adaptive-home-multi-household-overview.md)
   — a reversible IA choice, not an ADR — so contacts-by-phone is the sole ADR-0019.
5. **Switcher sheet overlap.** For multi-household users the Overview and the switcher
   sheet both change books; kept intentionally (the sheet is the only in-context switch
   from non-home screens). Revisit if it proves redundant.
6. Archived data (`scope=archived`, `archivedAt`, `isOwner`) and `restore` come from
   ADR-0016 — regenerate `packages/api-client` in the implementing PR when those land.
