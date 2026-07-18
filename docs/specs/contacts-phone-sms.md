# Contacts by phone number via blind SMS invites — spec

A contact list of friends/family you add by phone number, so you don't have to invite by
email every time. Adding a number sends a **blind SMS invite** — it never reveals whether
that number is already a Settl user — and a reusable contact edge is created only when the
invite is *accepted*. The load-bearing decisions, rejected alternatives, and privacy research
(ADR-0019, grilled 2026-07-16) are in the [Decision record](#decision-record-adr-0019) below.

Provenance: decided via `/grill` on 2026-07-16 (was ADR-0019). Adding phone touches identity,
a new comms vendor, and two minefields — contact discovery and GDPR — so the research
(Decision record → sources) settled the privacy posture before any product choice.

## Problem

Wishlist: "En kontaktlista med vänner/familj — lägg till varandra via telefonnummer, så man
slipper lägga till via mail." Today the only way to add someone is the email invite (ADR-0005:
tokenized link, 7-day expiry, Resend). There is no contacts/social graph — membership is
strictly per-household (ADR-0007) — and `Member.PhoneNumber` exists as an Identity column but
is never set, exposed, or used.

## Scope (v1)

- **Phone is a contact attribute, not an identity.** Email stays the sole login and uniqueness
  key (ADR-0005 unchanged). A member may set an optional phone number on their own profile. No
  SMS at signup, no phone-based login or recovery.
- **Add by number = a blind SMS invite, never a lookup.** Typing a number sends a tokenized SMS
  invite and reveals nothing about whether that number is already a Settl user. No
  registration-status endpoint exists → no enumeration oracle. Extends ADR-0005's `Invite` model
  with an SMS delivery channel; the raw typed number lives only on the invite row and is not
  retained after the invite expires.
- **Contacts graph is connection-on-accept.** A `Member`↔`Member` contact edge is created only
  when an invite is *accepted* — proving consent and number ownership. No stranger search, no way
  to materialize a contact from an unaccepted number. Saved contacts are reusable across
  households to pre-fill future invites (the wishlist's actual payoff). No friend requests,
  blocking, presence, or mutual-consent negotiation.
- **A self-entered profile phone is stored unverified (no OTP yet).** Ownership is proven at
  connect time by receiving the SMS invite link, so a profile field needs no OTP
  (tech-debt/0010). Later merged with the Swish number into one field per ADR-0026.
- **No SMS provider chosen now.** Spec the SMS invite channel but defer wiring a vendor until
  the feature is built (mirroring how email delivery was chosen ahead of sending code). When
  implemented, evaluate **Sinch** (Swedish, lowest Nordic rates, flash-call) and **Vonage**
  (strong Nordic delivery, ~20–30% under Twilio); Twilio is the DX benchmark but priciest.

## Data model

- `Invite` gains an **SMS channel** — a phone column and a delivery-type discriminator; the raw
  invitee number is transient (dropped on expiry).
- New **`Contact`** join entity (`Member`↔`Member` edge), created on invite accept.
- `Member.PhoneNumber` (Identity column) becomes settable — **unverified** until OTP
  (tech-debt/0010), so display/contact data only, never a lookup key or auth factor.
- Provider-portable EF Core (ADR-0010); API shape changes → regenerate `packages/api-client`.

## API surface

- Extend the invite flow with an SMS delivery type; the accept path creates the `Contact` edge.
- Profile write sets the optional `PhoneNumber` (E.164-normalized via `PhoneHelpers`).
- **Rate-limiting must ship *with* the SMS channel, not after** — each SMS costs money (unlike
  email, tech-debt/0006), and SMS pumping / artificially-inflated traffic is a documented fraud
  vector.

## Web surface

- Contacts screen: add-by-number affordance, saved contacts reusable across household invites.
- The multi-household overview carries the entry-point "add friend/contact" affordance
  ([adaptive-home-multi-household-overview.md](adaptive-home-multi-household-overview.md)).

## Decision record (ADR-0019)

The research settled three things before any product choice:

- **Hashing phone numbers gives no privacy.** Phone numbers are low-entropy; hashes are
  reversible in milliseconds. Signal removed hashing-based discovery in 2020. Address-book upload
  is the specific minefield.
- **Enumeration cannot be prevented, only rate-limited.** Any "is this number on Settl?" endpoint
  is an oracle; legitimate lookups must query the DB, so abuse is bounded only by rate limits.
- **Storing others' numbers makes Settl a GDPR data controller.** The household exemption doesn't
  cover an app, and consent from every number in an address book is impractical.

Hence: phone as contact attribute (not identity), blind SMS invite (no lookup), and
connection-on-accept graph. Storing the invitee's number transiently on the invite row and
discarding it on expiry keeps the GDPR surface small; the persistent graph only ever holds
relationships between consenting members, not scraped numbers.

*Explicitly rejected:* phone as a login/second identity (forces SMS on every signup, duplicates
email-centered uniqueness/recovery); a searchable user directory / number lookup (reintroduces
the enumeration oracle the evidence says cannot be closed); device address-book upload (no real
privacy from hashing, impractical third-party GDPR consent); deriving contacts purely from shared
households (too weak — can't help the *first* time you add someone).

## Out of scope / open questions

- **SMS provider selection** — deferred; evaluate Sinch/Vonage/Twilio when built.
- **Profile-phone OTP verification** — tech-debt/0010; unverified until then.
- Friend requests, blocking, presence — deliberately not built.

## Sources

- "All the Numbers are US: Large-scale Abuse of Contact Discovery in Mobile Messengers"
  (NDSS 2021) — hashing gives no privacy, enumeration only rate-limitable:
  https://www.ndss-symposium.org/wp-content/uploads/ndss2021_1C-3_23159_paper.pdf
- GDPR lawful basis / consent for third-party contact data:
  https://gdpr.eu/gdpr-consent-requirements/ and https://gdpr-info.eu/issues/consent/
- SMS OTP provider landscape (Sinch/Vonage/Twilio, Nordic delivery, flash-call, SMS pumping):
  https://www.messagecentral.com/blog/twilio-vs-vonage-vs-sinch-otp and
  https://apiscout.dev/guides/twilio-vs-vonage-vs-sinch-sms-api-2026
- Twilio Verify per-verification pricing:
  https://www.authgear.com/post/twilio-verify-pricing-and-alternatives/
