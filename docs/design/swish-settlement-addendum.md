# Swish settlement addendum

Design pass for the Swish "pay this debt" UI from
[swish-settlement-payments.md](../specs/swish-settlement-payments.md) (shipped in PR #59
with sensible defaults, flagged as a follow-up because it never got a design review —
design connectors needed interactive auth). This addendum records the **validated**
copy, placement, and trademark calls the spec deferred with "design to confirm". No new
`.dc.html` export: the feature adds no new screen or visual component — it's a launcher
action inside the existing settle-up sheet and one field on the existing profile screen —
so the decisions live here rather than in a Claude Design mock.

Surfaces:
- Settle-up sheet action `SwishPayAction` — `apps/web/src/components/sheets/settle-up-sheet.tsx`
- Profile field — `apps/web/src/routes/profil.tsx`

---

## 1. Settle-up action

**Placement — confirmed.** The action sits between the muted settle explainer and the
`Markera allt som reglerat` button, giving the top-to-bottom reading order *pay → then
mark settled*. It is rendered only when the API returns `swishPay` (debtor, SEK net > 0,
creditor has a saved number — ADR-0006 is authoritative).

- **Mobile:** a full-width tap-through link (`buttonVariants()`) to `swishPay.uri` that
  opens the Swish app pre-filled.
- **Desktop:** a QR card rendered from the same `uri` for scanning from a phone. The QR
  keeps **fixed** `#fff`/`#000` (not themeable tokens) so it stays scannable in both
  themes — a deliberate exception to the Tailwind-tokens-only rule.

**Adjacent explainer — reworded when the button is present.** The generic
`Betala som ni brukar — Swish, kontanter, banköverföring. Settl håller bara boken i
ordning.` line listed Swish as one option and then sat directly above a real Swish
button — mildly redundant and self-undercutting. Its load-bearing half (Settl doesn't
process the payment) matters *more* next to a pay button, since tapping it looks like it
might auto-settle. Resolution: keep the original line when there is **no** Swish button;
when there **is**, swap to a line that drops the methods list and reinforces the two-step
nature:

> Betala först — bocka sedan av. Settl bokför inget automatiskt.

**Copy — confirmed.**
- Button label: **`Betala med Swish`** (kept). Clear, unambiguous, and consistent with
  the `Swish` noun already used in the sheet; not churned to the verb `Swisha` despite
  Swish's own verb-forward branding, to avoid introducing a term used nowhere else.
- Desktop QR helper: trimmed to **`Skanna med Swish-appen`** (was `…för att betala`) —
  the card's `Betala med Swish` uplabel already states the purpose.

## 2. Profile field

**Confirmed as shipped.** Optional `Swish-nummer (valfritt)` field with the same `+46`
chip affordance as the profile phone input; cosmetic formatting only, the API validates
and stores E.164 (ADR-0006, ADR-0019). Help text:

> Låter andra i hushållet betala dig med Swish när ni gör upp. Sparas som det är — inte
> verifierat.

The "Sparas som det är — inte verifierat" clause is the plain-language trust disclosure
for unverified contact data ([tech-debt/0010](../tech-debt/0010-unverified-contact-data.md))
— keep it. Fits the shared-notebook voice (never fintech, never nagging).

## 3. Trademark — plain text, no logo

**Decision: use a plain-text label; do NOT adopt the Swish logo or official button.**
This confirms the spec's fallback ("else use a plain text label"). Rationale:

- The Swish logo is a **registered trademark**; commercial use requires **explicit
  written permission**, which Settl does not have.
- Swish's brand guidelines require **clear brand separation** — third parties must use
  their own visual identity and not replicate the Swish look. The "show our logo"
  encouragement targets businesses with a **Swish Handel merchant agreement**; Settl has
  none — it uses only the free consumer pre-fill link.
- A text label carries **zero brand obligation** (no clear-space / plate / no-modification
  rules) and no risk of implying a partnership or endorsement that doesn't exist.

Consequently there is **no Swish brand asset to mirror into `docs/design/`**. Revisit
only if a Swish Commerce/Handel agreement is ever taken on (out of scope per the spec).

Sources: [Swish — General guidelines](https://www.swish.nu/marketing-toolbox/general-guidelines),
[Swish Developer — Guidelines](https://developer.swish.nu/documentation/guidelines).

---

## 4. Copy glossary (sv → en, additions)

| sv | en |
|---|---|
| Betala med Swish | Pay with Swish (button / QR uplabel) |
| Skanna med Swish-appen | Scan with the Swish app (desktop QR helper) |
| Betala först — bocka sedan av. Settl bokför inget automatiskt. | Pay first — then check it off. Settl records nothing automatically. (settle explainer, Swish present) |
| Swish-nummer (valfritt) | Swish number (optional) |
| Låter andra i hushållet betala dig med Swish när ni gör upp. Sparas som det är — inte verifierat. | Lets others in the household pay you with Swish when you settle up. Saved as-is — not verified. |
| Ditt Swish-nummer | Your Swish number (aria-label) |
