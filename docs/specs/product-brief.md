# Settl — product brief

Household debt-tracking app. A shared ledger for any household — roommates, couples,
families — tracking who owes whom. Not a Splitwise clone.

**Tone:** lightweight and trust-based. Shared household notebook, not accounting
software. No corporate fintech, no adversarial "collections" energy.

**Platform:** web first with mobile-first layouts; native iOS (Expo) is the long-term
goal.

## Core functionality

- **Shared ledger, three entry types:**
  1. One-off shared expenses (groceries, dinner)
  2. Informal IOUs ("you owe me $20")
  3. Recurring shared costs (rent, subscriptions) that auto-renew and re-split each cycle
- **Flexible splitting:** equal by default; custom % or fixed amounts per person on any
  expense.
- **Open trust model:** any household member adds entries directly. No approval step.
- **Pure-ledger settlement:** no in-app payments. People pay however they already do and
  mark entries settled.
- **Two-level balance display:** net balance up top ("you owe Sam $12 overall"),
  full itemized history underneath.
- **Smart, event-triggered reminders** — not constant nagging. Triggers: large new
  expense, balance crossing a threshold, recurring due date approaching (e.g., rent).

## Differentiator

Best-in-class UX for **recurring/subscription splitting** — the main gap in existing
apps. Auto-renew and re-split each cycle must feel effortless, never like manual
re-entry every month.

## Open questions (do not over-commit in code)

- **Multi-household membership:** a person may belong to more than one household
  (family + roommates). Undecided — model user↔household as many-to-many from the
  start, but don't build UI for it yet.
- **Currency:** single currency per household is the assumed v1. Multi-currency
  undecided. Store amounts as integer minor units regardless.
- **Reminder channel:** email? push? in-app only? Undecided — decide with the
  reminders feature spec.
