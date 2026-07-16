# ADR-0019: Contacts by phone number via blind SMS invites

- **Status:** accepted
- **Date:** 2026-07-16

## Context

Wishlist: "En kontaktlista med vänner/familj — lägg till varandra via telefonnummer,
så man slipper lägga till via mail." Today the only way to add someone is the
email-only invite (ADR-0011): tokenized link, 7-day expiry, Resend for delivery.
There is no contacts/social graph — membership is strictly per-household (ADR-0007),
and `Member.PhoneNumber` exists as an Identity column but is never set, exposed, or
used. Adding phone touches identity, a new comms vendor, and two known minefields:
contact discovery and GDPR. Grilled 2026-07-16 with current sources (below).

The research settled three things before any product choice:

- **Hashing phone numbers gives no privacy.** Phone numbers are low-entropy; hashes
  are reversible in milliseconds. Signal removed hashing-based discovery in 2020 and
  moved to SGX enclaves. Address-book upload is the specific minefield.
- **Enumeration cannot be prevented, only rate-limited.** Any "is this number on
  Settl?" endpoint is an oracle; legitimate lookups must query the DB, so abuse is
  bounded only by rate limits, never closed.
- **Storing others' numbers makes Settl a GDPR data controller.** The household
  exemption does not cover an app, and consent from every number in an address book
  is impractical — forcing a shaky legitimate-interest balancing test.

## Decision

- **Phone is a contact attribute, not an identity.** Email stays the sole login and
  uniqueness key (ADR-0005/0011 unchanged). A member may set an optional phone number
  on their own profile. No SMS at signup, no phone-based login or recovery.
- **Add by number = a blind SMS invite, never a lookup.** Typing a number sends a
  tokenized SMS invite to it and reveals nothing about whether that number is already
  a Settl user. No registration-status endpoint exists → no enumeration oracle. This
  extends ADR-0011's `Invite` model with an SMS delivery channel; the raw typed number
  lives only on the invite row and is not retained after the invite expires.
- **Contacts graph is connection-on-accept.** A `Member`↔`Member` contact edge is
  created only when an invite is *accepted* — proving consent and number ownership.
  There is no stranger search and no way to materialize a contact from an unaccepted
  number. Saved contacts are reusable across households to pre-fill future invites,
  which is the wishlist's actual payoff. We deliberately do **not** build friend
  requests, blocking, presence, or mutual-consent negotiation.
- **A self-entered profile phone is stored unverified (no OTP yet).** Ownership is
  proven at connect time by receiving the SMS invite link, so a profile field needs no
  OTP. This avoids coupling the feature to an SMS provider (see below). Recorded as
  tech-debt/0010.
- **No SMS provider is chosen now.** We spec the SMS invite channel but defer wiring a
  vendor until the feature is built — mirroring how ADR-0011 chose email delivery ahead
  of any sending code. When implemented, evaluate **Sinch** (Swedish-based, lowest
  Nordic base rates, offers flash-call verification with no SMS body) and **Vonage**
  (strong Nordic alphanumeric-sender-ID delivery, ~20–30% under Twilio); Twilio is the
  DX benchmark but the priciest. Resend is email-only, so this is a second comms vendor.

## Consequences

`Invite` gains an SMS channel (a phone column and a delivery-type discriminator) and a
new `Contact` join entity plus edge-on-accept logic lands on the accept path; the API
shape changes, so `packages/api-client` regenerates in the implementing PR. Because
each SMS costs money (unlike email), the missing rate-limiting already accepted for
email (tech-debt/0006) becomes cost-critical for SMS — SMS pumping / artificially
inflated traffic is a documented fraud vector — so rate-limiting must ship *with* the
SMS channel, not after. The profile phone is unverified until OTP is added
(tech-debt/0010), so it is display/contact data only and must never become a lookup
key or auth factor while unverified. Storing the invitee's number transiently on the
invite row is minimal third-party data; discarding it on expiry keeps the GDPR surface
small, and the connection-on-accept design means the persistent graph only ever holds
relationships between consenting members, not scraped numbers.

Explicitly rejected: phone as a login/second identity (forces SMS on every signup,
duplicates ADR-0005's email-centered uniqueness/recovery, serves nothing in the
wishlist); a searchable user directory / number lookup (reintroduces the enumeration
oracle the evidence says cannot be closed); and device address-book upload (no real
privacy from hashing, and impractical third-party GDPR consent). Deriving contacts
purely from shared households was rejected as too weak — it cannot help the *first*
time you add someone.

## Sources

- "All the Numbers are US: Large-scale Abuse of Contact Discovery in Mobile Messengers"
  (NDSS 2021) — hashing gives no privacy, enumeration only rate-limitable:
  https://www.ndss-symposium.org/wp-content/uploads/ndss2021_1C-3_23159_paper.pdf
  and project site https://contact-discovery.github.io/
- GDPR lawful basis / consent for third-party contact data:
  https://gdpr.eu/gdpr-consent-requirements/ and https://gdpr-info.eu/issues/consent/
- SMS OTP provider landscape (Sinch/Vonage/Twilio, Nordic delivery, flash-call, SMS
  pumping): https://www.messagecentral.com/blog/twilio-vs-vonage-vs-sinch-otp and
  https://apiscout.dev/guides/twilio-vs-vonage-vs-sinch-sms-api-2026
- Twilio Verify per-verification pricing model:
  https://www.authgear.com/post/twilio-verify-pricing-and-alternatives/
- ADR-0005 (email as identity), ADR-0011 (invite model + Resend), ADR-0007 (per-household
  membership, no social graph), tech-debt/0006 (no rate limiting on auth sends).
