# Profile addendum

Extends `implementation-map.md` with the screens in **`Settl Profile.dc.html`** (new
export). Covers the wishlist item *"En profilsida där man kan justera lösen, byta
profilbild, byta namn."* Same conventions: UI structure from the DC export is
authoritative; API fields below are **proposed contracts** to build and regenerate into
`packages/api-client` in the same PR that consumes them.

**Binding decision — the [avatar-personalization spec](../specs/avatar-personalization.md)
(emoji, no image uploads):** the "profilbild" is an **optional emoji** rendered on the
member's existing email-derived `AvatarColor`, with the letter initial as the fallback
when unset. There is **no image upload**, no crop tool, and no color picker. Anything in
this addendum that implies file upload or stored image bytes is out of scope by that spec.

---

## 1. Route table (addition)

| Route | Screen (sv / en) | Purpose | Defined by |
|---|---|---|---|
| `/profil` | **Profil** / Profile | Change name, pick avatar emoji, change password, sign out | Settl Profile (frames 1–2) |
| `/profil` (avatar sheet) | **Välj profilbild** / Choose avatar | Emoji picker + "use my letter" reset | Settl Profile (frame 3), responsive sheet |
| `/profil` (password sheet) | **Byt lösenord** / Change password | Current + new + confirm | Settl Profile (frames 4–5), responsive sheet |

Part of `<AppShell>` (has the sidebar/tab-bar chrome), unlike the auth screens. The two
sub-actions are the **same `<ResponsiveSheet>` pattern** as the rest of the app (Drawer
on mobile / Dialog on desktop), driven by a search param on `/profil`
(e.g. `?sheet=avatar`, `?sheet=password`) — one open at a time.

**Access point:** reachable by tapping **your own avatar** in the mobile top header
(implementation-map §3), and from the **sidebar footer** on desktop (next to the theme
toggle). No new nav slot in the bottom tab bar.

---

## 2. Per-screen spec

### 2.1 Profil `/profil` (frames 1–2)

**Components**
- Header: back button + `Profil` uplabel (in-app chrome still around it).
- **Avatar block** — large 78px **person circle** (ADR-0016: person = circle) on the
  member's `AvatarColor`. Reuse/extend `MemberAvatar` (`apps/web/src/components/member-avatar.tsx`):
  render `avatarEmoji` centered when set, else the existing letter initial. Below it:
  - **Unset (frame 1):** one chip button `Välj profilbild` → avatar sheet.
  - **Set (frame 2):** two chips — `Byt emoji` → avatar sheet, and `Ta bort` → clears
    the emoji back to the initial (optimistic; confirm via save/toast).
- **Namn** — `flabel` + text `Input` prefilled with the current name; caption
  `Så här syns du i loggboken.` (reuses the auth "Ditt namn" helper wording).
- **Konto** card — read-only `E-post` row (value + muted `Din inloggning`) and a
  `Byt lösenord ›` row-link → password sheet. Email is display-only (email change is
  **not** in scope — not grilled).
- **Actions** — primary `Spara` (persists name + emoji); `Logga ut` as a
  `btn-danger-ghost`.

**Data (proposed)**
- `GET /me` → extend `MeDto` with `AvatarEmoji` (nullable) **and** `Email` (needed for
  the read-only row; not currently on `MeDto`). Today: `MeDto(Id, Name, AvatarColor,
  EmailConfirmed)` in `apps/api/Settl.Api/Dtos/AuthDtos.cs`.
- Save name + emoji: `PUT /me { name, avatarEmoji }` (or `PATCH`). `avatarEmoji: null`
  or empty → reset to initial. Returns the updated `MeDto`.
- Sign out: `POST /auth/logout` (session/cookie clear per ADR-0005).

**Validation** — name required → `Ange ditt namn.` (reuse). Emoji is validated
server-side: must be a **single emoji grapheme**, length-capped — it is untrusted text
rendered in *other* members' UIs (avatar-personalization spec). Reject non-emoji input.

**States** — Loading: skeleton avatar + field. Save in flight: disable `Spara`, spinner.
Success: toast `Profilen sparad`. Error: toast + keep form (implementation-map §7 / amb. 12).

### 2.2 Välj profilbild (avatar sheet, frame 3)

**Components** — responsive sheet titled `Välj profilbild` + sub
`Plocka en emoji — eller håll dig till din bokstav.`; live preview (78px circle on the
member color showing the currently-highlighted emoji); search `Input` (`Sök emoji`);
emoji grid (6-up, selected cell gets accent inset ring + soft bg); actions: primary
`Använd {emoji}` (commits the pick) and outline `Använd min bokstav istället` (clears the
emoji → initial fallback).

**Data** — no server call on open; the pick is applied via the profile `PUT /me` on
commit (or immediately with optimistic update). Toast `Profilbild uppdaterad`.

### 2.3 Byt lösenord (password sheet, frames 4–5)

**Components** — responsive sheet titled `Byt lösenord` + sub
`Fyll i ditt nuvarande lösenord och välj ett nytt.`; three `mono` password inputs —
`Nuvarande lösenord`, `Nytt lösenord` (caption `Minst 8 tecken.`), `Upprepa nytt
lösenord`; primary `Spara lösenord` (busy → `Sparar…`); outline `Avbryt`.

**Validation** (inline field errors, like the auth screens — errors must survive until
corrected, not toasts): wrong current → `Fel nuvarande lösenord.`; new < 8 chars →
`Lösenordet behöver minst 8 tecken.` (reuse); mismatch → `Lösenorden matchar inte.`.

**Data (proposed)** — `POST /account/change-password { currentPassword, newPassword }`
→ ASP.NET Identity `UserManager.ChangePasswordAsync` (ADR-0005). 204 on success;
400 + error code the client maps to the field messages above. On success: close sheet,
toast `Lösenordet uppdaterat`.

---

## 3. Copy glossary (sv → en, additions)

| sv | en |
|---|---|
| Profil | Profile |
| Välj profilbild | Choose avatar |
| Plocka en emoji — eller håll dig till din bokstav. | Pick an emoji — or stick with your letter. |
| Byt emoji / Ta bort | Change emoji / Remove |
| Sök emoji | Search emoji |
| Använd {emoji} | Use {emoji} |
| Använd min bokstav istället | Use my letter instead |
| Profilbild uppdaterad | Avatar updated (toast) |
| Namn | Name |
| Så här syns du i loggboken. | This is how you appear in the ledger. (reused) |
| Konto | Account |
| E-post | Email |
| Din inloggning | Your sign-in |
| Byt lösenord | Change password |
| Fyll i ditt nuvarande lösenord och välj ett nytt. | Enter your current password and choose a new one. |
| Nuvarande lösenord | Current password |
| Nytt lösenord | New password |
| Upprepa nytt lösenord | Repeat new password |
| Minst 8 tecken. | At least 8 characters. (reused) |
| Spara lösenord / Sparar… | Save password / Saving… |
| Fel nuvarande lösenord. | Wrong current password. |
| Lösenorden matchar inte. | The passwords don't match. |
| Lösenordet behöver minst 8 tecken. | The password needs at least 8 characters. (reused) |
| Lösenordet uppdaterat | Password updated (toast) |
| Spara | Save |
| Profilen sparad | Profile saved (toast) |
| Logga ut | Sign out |
| Ange ditt namn. | Enter your name. (reused) |

---

## 4. Notes & open questions

1. **API-shape changes (regenerate `packages/api-client` in the implementing PR):**
   `MeDto` gains `AvatarEmoji` + `Email`; `Member` gains a nullable `AvatarEmoji` column
   (new EF migration); new endpoints `PUT /me`, `POST /account/change-password`,
   `POST /auth/logout` (confirm which already exist).
2. **`AvatarEmoji` propagation:** `MemberDto` and the entry/household member DTOs
   (`HouseholdDtos.cs`, `EntryDtos.cs`) that currently carry `AvatarColor` should also
   carry `AvatarEmoji`, so avatars render consistently across Home/Ledger/detail — not
   just on the profile page. `MemberAvatar` renders emoji-over-color with initial
   fallback everywhere.
3. **Emoji validation is security-relevant, not cosmetic** (avatar-personalization spec): enforce
   single-grapheme + length cap server-side; the client picker constrains input but the
   API is authoritative (ADR-0006).
4. **Emoji picker implementation — dependency decision (open):** the design shows a
   searchable grid. A no-dependency curated grid (a fixed common-emoji set, no search) is
   the cheapest and needs no new package; a full searchable picker implies an emoji
   dataset/library (e.g. `frimousse` / `emoji-picker-element`) → **state the why first**
   per the root no-new-deps rule. Decide before building; the export intentionally shows
   both a search field and a small grid so either reading is possible.
5. **Email change is out of scope** — shown read-only. If wanted later it needs its own
   decision (re-confirmation / verification email), not this screen.
6. **`Logga ut` placement** is a natural add for a profile/account page (not in the
   original wishlist text); backed by existing Identity auth (ADR-0005). Included because
   an account page without sign-out is odd — flag if it should move to the shell instead.
7. Theming: the export uses the mint harness palette; production uses the single
   light+dark token set (implementation-map ambiguity #8 applies).
