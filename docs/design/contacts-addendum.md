# Contacts addendum (2026-07-16)

New file: **`Settl Contacts.dc.html`** — contacts list + add-by-phone (blind SMS
invite). All other design files unchanged. Design realises **ADR-0019**; read that
first — the ADR fixes the product decisions, this maps screens to a **proposed** API
contract (none of it exists yet; grill-and-spec only).

## Design decisions carried from ADR-0019

- **Person = circle, household = rounded square** (matches `Settl Household
  Management.dc.html`). Contacts are people, so all avatars here are circles.
- **No enumeration oracle in the UI.** "Add contact" never shows whether a number is
  already on Settl. The privacy note (screen 2, 🔒) states this explicitly. There is no
  search-users field anywhere.
- **Connection-on-accept.** A contact only appears in the list once an invite is
  accepted; before that it sits under **Väntar på svar** as the raw number, not a
  person. Screen 1 shows both states.
- **Phone is a contact attribute, not identity.** Profile screen (5) labels the field
  *valfritt* and *Overifierat*, with the line "E-post är fortfarande din inloggning."
  No OTP on save (tech-debt/0010).

## Screens → proposed API

1. **Kontaktlista** — `GET /contacts` → accepted contacts (member id, name,
   avatarColor, `sharedHouseholdCount`) + `GET /contacts/pending` (or one payload with
   a status field) → outstanding invites the current user sent (masked number, channel,
   sentAt, expiresAt). Contacts are the `Member↔Member` edge from ADR-0019; "I N hushåll
   med dig" is derived server-side (ADR-0006 — UI renders, never computes).
2. **Lägg till kontakt** — `POST /contacts/invites` `{ channel: "sms" | "email",
   phone?: "+46701234567", email?: string }`. Extends ADR-0011's `Invite` with a
   channel + phone column. **No household id** when invited from the contacts tab
   (contact-only); include a household id when invited from screen 4. Response reveals
   nothing about existing registration — always the same "sent" shape. E.164 normalise +
   validate server-side.
3. **Inbjudan skickad** — client-side confirmation of the 201 from screen 2. Copy states
   the 7-day expiry (reuse ADR-0011's `InviteLifetime`). No "user exists" branch.
4. **Bjud in till hushåll** — `GET /households/{id}/invitable-contacts` → saved contacts
   with per-contact status for THIS household (`member` = already joined, `invitable` =
   pick to invite, `pending`). Selecting + confirm → `POST /households/{id}/invites` per
   picked contact (existing endpoint) or the contact-linked variant. "+ Ny person via
   telefon/e-post" falls through to screen 2 with the household id attached. **This is
   the wishlist payoff — reuse a saved contact instead of re-typing.**
5. **Profil** — `PATCH /me` `{ phone?: "+46735551234" }`, stored on
   `Member.PhoneNumber` (the existing-but-unused Identity column). Stored **unverified**
   → surface the "Overifierat" pill; never used as a lookup key or auth factor while
   unverified (ADR-0019 / tech-debt/0010).
6. **Tomt läge** — empty `GET /contacts`; CTA opens screen 2.

## Follow-ups for the implementing PR

- API-shape change → regenerate `packages/api-client` (root rule).
- SMS provider is **deferred** (ADR-0019): screens 2–3 assume an `ISmsSender`
  abstraction mirroring `IEmailSender`, wired to no real vendor yet. Rate-limiting must
  ship **with** the SMS channel — each SMS costs money, unlike email (ADR-0019
  consequences; tech-debt/0006 is the email precedent).
- Accepting an invite must create the `Member↔Member` contact edge on the accept path
  (extends `AcceptInvite` in `InvitesEndpoints.cs`).
