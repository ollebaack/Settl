# Auth & onboarding addendum

Extends `implementation-map.md` with the screens in **`Settl Sign In.dc.html`** (new export, 2026-07-13). Same conventions: UI structure from the DC export is authoritative; API fields below are **proposed contracts** pending ADR treatment (auth is ASP.NET Identity per ADR-0005 — these screens are its UI).

**Terminology change (affects existing docs):** Settl's main audience is households, but copy now says **grupp** where the container is generic (creation, invites) and keeps **hushåll** where a concrete household is referenced. The brand tagline is now `Den delade anteckningsboken — för hushållet, resan eller gänget` ("The shared notebook — for the household, the trip, or the crew"). Existing in-app copy ("Dina hushåll" etc.) is unchanged for now — flag for a follow-up copy pass.

---

## 1. Route table (additions)

| Route | Screen (sv / en) | Purpose | Defined by |
|---|---|---|---|
| `/signin` | **Logga in** / Sign in | Email + password, forgot password, magic link | Settl Sign In (`screen=signin`) |
| `/signin` (sent state) | **Kolla din inkorg** / Check your inbox | Confirmation after magic/reset link | Settl Sign In (view `sent`) |
| `/signup` | **Skapa konto** / Sign up | Name, email, password — no group at signup | Settl Sign In (`screen=signup`) |
| `/invite` | **Bjud in din grupp** / Invite your group | Share link + email invites after group creation | Settl Sign In (`screen=invite`) |
| `/inbjudan/{code}` | **Acceptera inbjudan** / Accept invite | Invitee creates an account and joins in one step | Settl Sign In (`screen=accept`) |
| (overlay) | **Skapa din första grupp** / Create your first group | Post-auth modal when the user has no group | Settl Sign In (`showNoHH` modal) |

All auth screens share one centered layout: logo block (52px accent square `S`, wordmark, tagline), one 380px card on `--bg`, contextual footer link. Not part of `<AppShell>` — no sidebar/tab bar.

---

## 2. Per-screen spec

### 2.1 Logga in `/signin`

**Components** — card title `Välkommen tillbaka` + sub `Logga in för att se hur ni ligger till.`; error banner (chip bg, destructive text) above fields; `E-post` + `Lösenord` inputs (uppercase 11.5px labels, 12px radius, bg fill); inline `Glömt?` link on the password label row; primary button `Logga in` (48px, spinner while busy → `Loggar in…`); `eller` divider; secondary soft-bg button `✉ Skicka inloggningslänk` (magic link). Footer: `Ny på Settl? Skapa konto`.

**Validation** (inline error banner, not toasts): email must contain `@` → `Ange en giltig e-postadress.`; empty password → `Ange ditt lösenord.`; magic link / forgot without email → `Fyll i din e-post först, så skickar vi en länk.` / `Fyll i din e-post så skickar vi en återställningslänk.`

**Post-auth branch:** if the user belongs to ≥1 group → app (`/`). If **zero groups** → the **first-group modal** (§2.5) over the sign-in screen.

**Sent state** — replaces card content: check tile, `Kolla din inkorg`, `Vi har skickat en inloggningslänk till {email}` (email in mono), `← Tillbaka till inloggning`. Used by both magic link and forgot-password.

**Data (proposed)** — `POST /auth/login { email, password }`; `POST /auth/magic-link { email }`; `POST /auth/forgot-password { email }`. Login response includes `groups: []` so the client can branch to the modal.

### 2.2 Skapa konto `/signup`

**Components** — title `Skapa konto` + sub `Ett konto per person — hushåll och grupper delar ni på sen.`; fields `Namn` (placeholder `Förnamn räcker`), `E-post`, `Lösenord` (placeholder `Minst 8 tecken`); primary button `Skapa konto` (busy → `Skapar konto…`); terms caption `Genom att skapa konto godkänner du villkoren.` Footer: `Har du redan ett konto? Logga in`.

**No group name at signup** — the account is personal; group creation happens in the post-auth modal (§2.5). Validation: name required → `Ange ditt namn.`; email `@`; password ≥ 8 chars → `Lösenordet behöver minst 8 tecken.`

**Flow:** success → toast `Konto skapat — välkommen till Settl` → first-group modal (§2.5).

**Data** — `POST /auth/register { name, email, password }` → 201 + session; zero groups by definition.

### 2.3 Bjud in din grupp `/invite`

Shown right after group creation (from the modal), or reachable later from group settings.

**Components** — title `Bjud in din grupp` + sub `Alla i gruppen ser samma loggbok.`; group chip (soft bg: accent initial square, group name, member count `1 medlem` / `{n} pers`); **share link row** (1.5px dashed border, mono URL `settl.se/inbjudan/{code}`, chip button `Kopiera` → clipboard + toast `Länk kopierad`); **email invite row** (input + accent `Skicka` button) → appends a pending row (initial tile, email, pill `Väntar`) + toast `Inbjudan skickad till {email}`; primary `Klart — till appen`; ghost `Hoppa över så länge` → toast `Du kan bjuda in senare under Grupper`.

**Data** — `POST /groups/{id}/invites { email? }` → `{ code, url, email, status: "pending" }`; `GET /groups/{id}/invites` for the pending list. One reusable link-code per group + optional per-email invites.

### 2.4 Acceptera inbjudan `/inbjudan/{code}`

The invite-link landing page. **Accepting always creates an account** — the invitee has none yet (an existing user signs in instead via the footer link, then the code is applied to their account).

**Components** — centered header: inviter initial tile, `{Inviter} har bjudit in dig`, sub `till hushållet {namn} · {n} medlemmar`; member preview row (overlapping avatar squares + `{A} och {B} delar redan på loggboken.`); divider label `Skapa ditt konto för att gå med`; fields `Ditt namn` (placeholder `Så här syns du i loggboken`), `E-post`, `Lösenord`; primary `Skapa konto & gå med` (busy → `Skapar konto…`); ghost `Avböj inbjudan`. Footer: `Har du redan ett konto? Logga in`.

**Validation** — name → `Ange ditt namn först.`; email `@`; password ≥ 8. Success toast `Välkommen till {grupp}, {namn}`.

**Data** — `GET /invites/{code}` → `{ groupName, inviterName, memberPreview[] }` (public, pre-auth); `POST /invites/{code}/accept { name, email, password }` → creates Member + membership atomically; `POST /invites/{code}/decline`.

### 2.5 Skapa din första grupp (modal)

Appears over sign-in/sign-up when an authenticated user has **zero groups**. Scrim `rgba(10,14,11,.45)`, 400px card, `×` close (chip bg, top-right — dismissable; user can idle without a group).

**Components** — house-glyph tile (soft bg); title `Välkommen! Skapa din första grupp`; body `En grupp är er delade loggbok — för de flesta är det hushållet. Skapa den och bjud sen in de du bor med.`; input `Gruppens namn` (placeholder `t.ex. Södergatan 12 eller Fjällresan`); primary `Skapa grupp`; footer caption `Har någon redan skapat er grupp? Be dem bjuda in dig — du får en länk via e-post och behöver inte skapa något här.`

**Validation** — empty name → `Ge gruppen ett namn först.` Success → close modal, toast `Hushållet {namn} är skapat`, continue to `/invite` (§2.3).

**Data** — `POST /households { name }` (existing contract; `newMemberNames` empty — invites replace direct member entry). **Supersedes** implementation-map §2.5b's helper text `I riktiga appen får medlemmarna en inbjudan — här läggs de till direkt.` — the invite flow now exists; tech-debt 0001/0003 partially resolved on the design side.

---

## 3. Flows

- **Sign in (has group):** `/signin` → validate → busy ~1s → app.
- **Sign in (no group):** `/signin` → auth OK → first-group modal → `Skapa grupp` → `/invite` → `Klart` → app.
- **Sign up:** `/signup` → account created (toast) → first-group modal → as above.
- **Magic link / forgot password:** email filled → sent state → (out of band) → app.
- **Invite via link:** member copies `settl.se/inbjudan/{code}` → invitee opens `/inbjudan/{code}` → creates account + joins in one step → app.
- **Invite via email:** email → pending `Väntar` row → invitee gets the same link.

---

## 4. Copy glossary (sv → en, additions)

| sv | en |
|---|---|
| Den delade anteckningsboken — för hushållet, resan eller gänget | The shared notebook — for the household, the trip, or the crew (NEW tagline) |
| Välkommen tillbaka | Welcome back |
| Logga in för att se hur ni ligger till. | Sign in to see where you stand. |
| E-post / Lösenord / Namn | Email / Password / Name |
| Glömt? | Forgot? |
| Logga in / Loggar in… | Sign in / Signing in… |
| eller | or |
| Skicka inloggningslänk | Send sign-in link |
| Kolla din inkorg | Check your inbox |
| Vi har skickat en inloggningslänk till {email} | We've sent a sign-in link to {email} |
| ← Tillbaka till inloggning | ← Back to sign-in |
| Ny på Settl? Skapa konto | New to Settl? Create an account |
| Skapa konto / Skapar konto… | Create account / Creating account… |
| Ett konto per person — hushåll och grupper delar ni på sen. | One account per person — you share households and groups later. |
| Förnamn räcker | First name is enough |
| Minst 8 tecken | At least 8 characters |
| Genom att skapa konto godkänner du villkoren. | By creating an account you accept the terms. |
| Har du redan ett konto? Logga in | Already have an account? Sign in |
| Bjud in din grupp | Invite your group |
| Alla i gruppen ser samma loggbok. | Everyone in the group sees the same ledger. |
| Dela länk / Eller via e-post | Share link / Or by email |
| Kopiera / Skicka | Copy / Send |
| Väntar | Pending |
| Klart — till appen | Done — to the app |
| Hoppa över så länge | Skip for now |
| {namn} har bjudit in dig | {name} has invited you |
| till hushållet {namn} · {n} medlemmar | to the household {name} · {n} members |
| {A} och {B} delar redan på loggboken. | {A} and {B} already share the ledger. |
| Skapa ditt konto för att gå med | Create your account to join |
| Ditt namn / Så här syns du i loggboken | Your name / This is how you appear in the ledger |
| Skapa konto & gå med | Create account & join |
| Avböj inbjudan | Decline invite |
| Välkommen! Skapa din första grupp | Welcome! Create your first group |
| En grupp är er delade loggbok — för de flesta är det hushållet. Skapa den och bjud sen in de du bor med. | A group is your shared ledger — for most people it's the household. Create it, then invite the people you live with. |
| Gruppens namn / t.ex. Södergatan 12 eller Fjällresan | Group name / e.g. Södergatan 12 or the Ski trip |
| Skapa grupp | Create group |
| Ge gruppen ett namn först. | Give the group a name first. |
| Ange en giltig e-postadress. / Ange ditt lösenord. / Ange ditt namn. / Ange ditt namn först. | Enter a valid email. / Enter your password. / Enter your name. / Enter your name first. |
| Lösenordet behöver minst 8 tecken. | The password needs at least 8 characters. |
| Fyll i din e-post först, så skickar vi en länk. | Fill in your email first and we'll send a link. |
| Fyll i din e-post så skickar vi en återställningslänk. | Fill in your email and we'll send a reset link. |
| Konto skapat — välkommen till Settl | Account created — welcome to Settl (toast) |
| Länk kopierad / Inbjudan skickad till {email} | Link copied / Invite sent to {email} (toasts) |
| Du kan bjuda in senare under Grupper | You can invite later under Groups (toast) |
| Hushållet {namn} är skapat | The household {name} is created (toast) |
| Välkommen till {grupp}, {namn} | Welcome to {group}, {name} (toast) |
| Inbjudan avböjd | Invite declined (toast) |

---

## 5. Notes & open questions

1. **Auth errors are inline banners, not toasts** — deliberate departure from the in-app toast convention; auth errors must survive until corrected.
2. **Invite model is new to the API contract** — needs entities (`Invite { code, groupId, email?, status }`) and the four endpoints in §2.3–2.4. Flag for API grill.
3. **`newMemberNames` on `POST /households`** should be deprecated in favor of invites (supersedes §2.5b helper copy).
4. **"Grupp" vs "hushåll"** — decide whether the in-app switcher (`Dina hushåll`) renames to `Dina grupper` or households stay a named group type. Deferred.
5. The DC's `screen` tweak prop (`signin/signup/invite/accept`) is a preview control only — real navigation is routes.
6. Theming: the export reuses the six harness palettes; production auth pages use the same single token set as the app (implementation-map ambiguity #8 applies).
