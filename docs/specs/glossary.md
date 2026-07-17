# Glossary — spec

An in-app reference for Settl's own vocabulary (Lån, Reglerad, På repeat, …) so a
new household member can look up an unfamiliar term without asking someone else
or leaving the screen they're on.

## Problem

The app's copy uses a handful of terms that don't mean what they'd mean
elsewhere, or that are shorthand for a rule (e.g. **Lån** covers any informal
IOU, not a bank loan; **Reglerad** is a derived state, not something you set
directly — ADR-0006). New members currently have no way to check what a term
means except asking whoever invited them.

## Scope (v1)

- **Content:** a static, hardcoded list of term → plain-language definition,
  in Swedish (the app has one locale today). Lives in code
  (`apps/web/src/lib/glossary.ts`), maintained by whoever changes the copy it
  describes — not a CMS, not user-editable.
- **Entry point:** one new item in the account dropdown (`account-menu.tsx`),
  e.g. **Ordlista**, opening a sheet via the existing `useSheet()` pattern
  (same mechanism as `households`, `add`, etc.).
- **Presentation:** a single scrollable list inside `ResponsiveSheet` —
  term + definition, alphabetical. No search/filter at this term count (~12).
- **Terms covered (draft — confirm against current copy before building):**
  - Post — any ledger line: expense, IOU, or recurring post.
  - Utgift — a one-off shared expense.
  - Lån — an informal IOU between two members.
  - På repeat — a recurring cost that auto-posts and re-splits each cycle.
  - Loggbok — the full itemized history of posts.
  - Delas lika / Egna procent / Egna belopp — the three split modes.
  - Gör upp — record that a debt was paid outside the app.
  - Reglerad — a post whose balance is settled; derived, not user-set.
  - "Du är skyldig totalt" / "Du ska få totalt" — net balance across the
    whole household, not per person.
  - Aktivitet / Notiser — the reminders feed and the reminders themselves.
  - Hushåll — the group whose ledger you're viewing; a person can belong to
    more than one.

## Non-goals (v1)

- No inline "?" affordances or tooltips next to individual terms in other
  screens — this spec is the standalone reference only. Wiring individual
  terms to it is a follow-up if it turns out people don't find the account
  menu entry.
- No localization — glossary text ships in Swedish only, matching the rest
  of the app today.
- No search — revisit if the term count grows enough to need it.

## Open questions

- Does **Ordlista** belong in the account dropdown, or does it want its own
  nav affordance once there's a broader "Help/Settings" surface? Account menu
  is the only existing home for app-level (non-household) actions today, so
  default to there for v1.
- Do entry-detail / add-entry sheets ever want a direct link into a specific
  term (e.g. tapping "Lån" opens the glossary scrolled to that entry)? Deferred
  per non-goals above — revisit if support questions keep coming up.

## Out of scope for this spec

Anything about *content ops* — who reviews definitions when copy changes,
whether definitions need a lint/CI check against actual UI strings — is a
process question, not a product one. Flag as tech debt if it becomes a
recurring source of drift.
