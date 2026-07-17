# ADR-0026: One member phone number for both contact and Swish

- **Status:** accepted
- **Date:** 2026-07-17
- **Amends:** ADR-0019 (drops its "profile phone distinct from Swish number" stance);
  supersedes the "distinct from `PhoneNumber`" clause in the swish-settlement-payments spec.

## Context

A member could enter a number in two places — a "profile phone" on the Contacts screen
(Identity `PhoneNumber`, PATCH /me, ADR-0019) and a "Swish-nummer" on Profile (custom
`SwishNumber`, PUT /me, swish spec) — each a separate column, each E.164-normalized the
same way. Both were deliberately kept distinct because "a Swish number isn't necessarily
the account phone." In practice this shipped as two inputs on two screens for what users
read as one number, and the profile phone drives nothing today while the deferred
contacts/SMS feature (ADR-0019) that would use it has no UI yet. Research confirms a
private Swish number *can* differ from a contact mobile (up to 3 numbers per person;
multi-bank/multi-device users need different numbers) but this is an edge case for a
household expense app.

## Decision

We will store **one** member number in Identity's `PhoneNumber` and drop `SwishNumber`.
The Swish pay link reads `PhoneNumber`; the Profile screen exposes a single "Ditt nummer"
field (helper text notes it powers Swish today), and the Contacts phone input is removed.
The migration copies `SwishNumber` → `PhoneNumber` wherever a Swish number is set (Swish
value wins, since it drives a live payment feature), keeps `PhoneNumber` otherwise, then
drops the column. `PhoneNumberConfirmed` stays authoritative for the deferred OTP
(tech-debt/0010), so verification can land with no schema change.

## Consequences

One field, one screen, one column — the deferred contacts/SMS + OTP feature can light up
on the same stored number with no new UI, which was the goal. We accept that the rare
member whose Swish number differs from their contact mobile can now store only one of
them; a wrong-number typo still pays the wrong person (unchanged — tech-debt/0010). The
number is now framed generally ("Ditt nummer") rather than as a Swish-only field, so its
future reuse by the contacts feature is not a consent surprise. The API shape changes
(`swishNumber` leaves the /me DTOs; `phone` becomes the single write path), so
`packages/api-client` regenerates in the implementing PR. Revisit if the product ever
needs a genuinely separate payee number (e.g. business Swish 123-numbers, which the
private pre-fill path does not cover anyway).

## Sources

- Swish FAQ — numbers per person / per bank account (divergence is possible but bounded):
  https://www.swish.nu/faq/private/how-many-phone-numbers-can-i-connect-to-swish
  and https://en.mobil.se/how-tos/more-than-one-mobile-number-connected-to-swish-heres-how/1602746
- ADR-0019 (contacts by phone, profile phone reserved for it), swish-settlement-payments
  spec (SwishNumber added distinct), tech-debt/0010 (unverified number, OTP deferred).
