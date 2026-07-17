# Swish settlement payments — spec

Give a debtor a one-tap way to actually *pay* a settled-up debt via Swish, without Settl
taking on any paid service, bank agreement, or certificate. When you owe someone in a
household and they've saved a Swish number, Settl shows a **"Betala med Swish"** action on
the Settle-up sheet that opens the Swish app pre-filled with the exact amount and a
reference — or a QR to scan from another phone. Settl never learns whether the payment
went through: this is a convenience launcher, not a payment processor. Settlement stays the
existing manual "mark settled" action ([ADR-0007](../adr/0007-settlements-as-ledger-events.md),
[settlement-history.md](settlement-history.md)).

Provenance: decided via `/grill` on 2026-07-17. **Not ADR-worthy** — the only load-bearing
call (self-generated pre-fill link, *not* the paid Commerce API) is a cost-driven scoping
decision that's reversible: a future Commerce/PISP integration can be added alongside without
undoing this. The rejected alternatives and sources are recorded below in lieu of an ADR.

## Problem

Settl computes who owes whom (pure-ledger settlement) and lets a pair mark a debt settled,
but the money still has to move in some other app entirely. The user copies an amount, opens
their bank or Swish, finds the recipient, types the number and amount, pays, then comes back
to Settl and marks it settled. Every one of those steps is a chance to send the wrong amount
to the wrong person. Nothing in `apps/web/src/components/sheets/settle-up-sheet.tsx` bridges
"here's what you owe" to "here's how you pay it."

## Scope (v1)

- **Free path only.** Settl constructs the Swish **pre-fill link** itself —
  `https://app.swish.nu/1/p/sw/?sw=<number>&amt=<amount>&msg=<message>` — per the public Swish
  QR specification. No API token, no certificate, no "Swish Handel" merchant agreement, no fee.
- **Direction-aware.** The action appears only to the **debtor** (the person who owes), on the
  Settle-up sheet for the creditor they owe. The creditor never sees a "pay yourself" button.
- **Opt-in recipient number.** Shown only when the **creditor has saved a Swish number**
  (see Data model). No saved number → no button, no fallback.
- **SEK only.** Swish is SEK-only. The action is gated to SEK debts; hidden for any non-SEK
  net (forward-safe even though the ledger is SEK today — home market is SEK/sv-SE).
- **Locked amount and message.** The `edit` parameter is omitted, so Swish locks both fields.
  The payer cannot alter the amount or reference — the ledger figure is authoritative.
- **No confirmation, manual settle.** The free path has no callback; Settl makes no claim the
  payment happened. Marking the debt settled remains the existing separate action. No auto-settle,
  no "did you pay?" prompt in v1.
- **Not** the Swish Commerce/Handel API, **not** m-commerce token/callback flows, **not** PSD2
  bank payment initiation, **not** recurring/scheduled payments — see Out of scope.

## Data model

- New optional column **`Member.SwishNumber`** (`string?`, stored **E.164**), reusing
  `Services/PhoneHelpers.TryNormalize` and following [ADR-0019](../adr/0019-phone-e164.md).
  Absence = opted out. Distinct from the existing `Member.PhoneNumber` (a Swish number isn't
  necessarily the account phone; we deliberately do **not** derive one from the other).
- Same trust posture as `PhoneNumber`: **unverified contact data** — never a lookup key, auth
  factor, or proof of anything ([tech-debt/0010](../tech-debt/0010-unverified-contact-data.md)).
- EF Core migration adds the nullable column; no backfill. Provider-portable, no Postgres-specific
  types (ADR-0010).

## API surface

- **Profile write:** extend the existing profile-update endpoint (the one that already sets
  display name / avatar) to accept an optional `swishNumber`. The API normalizes via
  `PhoneHelpers.TryNormalize`, stores E.164, and rejects unparseable input with ProblemDetails.
  Empty/null clears it.
- **Pay link (read):** the API is authoritative for building the URL (ADR-0006 — the SPA renders,
  never computes). Surface a **`swishPay`** object on the Settle-up net read model for the pair:
  `{ uri, amountMinor }` when *all* hold — acting user is the debtor, net > 0, currency is SEK,
  and the creditor has a `SwishNumber` — otherwise `null`. The API:
  - formats öre → SEK decimal (`10050` → `100.50`) for `amt`,
  - renders `sw` as the E.164 number **without the leading `+`** (`+46701234567` → `46701234567`),
  - builds `msg` = `"Settl <household name>"` **sanitized** to Swish's allowed set
    (`a-öA-Ö0-9` and `!?(),.-:;` plus space), disallowed characters (emoji, etc.) dropped, then
    **truncated to 50 chars**,
  - URL-encodes and returns the full `https://app.swish.nu/1/p/sw/...` string.
- Money in integer minor units end-to-end; amounts server-derived (ADR-0006). New/changed
  endpoints get a `WebApplicationFactory` integration test (API rule), including the
  message-sanitization and öre→SEK formatting edge cases (unit-tested per API money rule).
- **Regenerate `packages/api-client`** in the same PR (root rule).

## Web surface

- **Profile:** an optional "Swish-nummer" field on the profile edit surface (alongside
  display name / avatar), with the existing `+46` phone-input affordance. Cosmetic formatting
  only; the API validates (ADR-0006).
- **Settle-up sheet** (`apps/web/src/components/sheets/settle-up-sheet.tsx`): when `swishPay` is
  present, render a **"Betala med Swish"** action near the net owed:
  - **On mobile**, a tap-through link to `swishPay.uri` — opens the Swish app pre-filled.
  - **On desktop**, a **QR** rendered from the same `uri` for the user to scan with their phone.
  - Copy stays adjacent to the manual "mark settled" control so the two-step nature (pay, then
    settle) is obvious. Confirm final Swedish wording and placement with design.
- Query hook in `apps/web/src/lib/queries.ts`, keyed by household + person, reusing the existing
  net query rather than a second round-trip where possible.
- **Trademark:** the Swish name/logo has usage guidelines; if the button uses the Swish mark it
  must follow them — design to confirm, else use a plain text label.

## Out of scope / open questions

- **Payment confirmation / auto-settle** — impossible on the free path (no callback). Requires
  the paid Swish Commerce API. Deferred; revisit only if a business agreement is ever justified.
- **Swedish bank account-to-account (PSD2 payment initiation)** — no free consumer deep-link
  exists; needs a PISP license or a paid aggregator (Tink/Visa, commission-based). Explicitly out.
- **QR generation dependency** — desktop QR needs either a small client-side QR library (**new
  dependency — needs sign-off per the root rule, state why before adding**) or a call to Swish's
  free pre-fill QR image endpoint (`mpc.getswish.net/qrg-swish/api/v1/prefilled`, an external
  runtime dependency). Default to the client-side lib to avoid an external call per render;
  rationale and pay-down trigger recorded in
  [tech-debt/0011](../tech-debt/0011-swish-qr-client-dependency.md).
- **Swish number verification** — v1 trusts the saved number blindly (tech-debt/0010). No
  ownership check; a typo pays the wrong person. Acceptable for a convenience launcher.
- **Non-SEK settlement** — no rail; the action is simply hidden. No FX.
- Exact Swedish copy for the button/field labels and the profile field help text — design to confirm.

## Sources

- [Swish — Generate QR codes / pre-fill spec](https://developer.swish.nu/documentation/guides/generate-qr-codes)
  (`sw`, `amt`, `msg`, `edit`; self-generation permitted; private numbers work)
- [Guide: Swish QR Code specification v1.7.2 (PDF)](https://assets.ctfassets.net/zrqoyh8r449h/12uwjDy5xcCArc2ZeY5zbU/ce02e0321687bbb2aa5dbf5a50354ced/Guide-Swish-QR-code-design-specification_v1.7.2.pdf)
  (base URL `https://app.swish.nu/1/p/sw/`, 50-char message limit, allowed charset)
- [Swish Commerce API setup](https://developer.swish.nu/documentation/getting-started/swish-commerce-api)
  and [Technical & legal requirements for Swish Handel — CE Sweden](https://www.ce.se/the-technical-and-legal-requirements-for-accepting-swish-handel-payments/)
  (merchant agreement, certificates, per-bank fees — rejected)
- [Open Banking in Sweden](https://www.openbankingtracker.com/country/sweden) and
  [Tink](https://tink.com/) (PSD2 payment initiation needs a license or paid aggregator — rejected)
