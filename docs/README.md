# Settl docs

How knowledge is organized in this repo:

| Folder | What lives here | Rule |
| --- | --- | --- |
| [adr/](adr/) | Architecture Decision Records | Short. **Cross-cutting, hard-to-reverse architecture/infra decisions only** — the stack, the API contract, the data model, deployment, auth. Kept to a small set (~10). Written as the outcome of a `/grill` session, never casually. |
| [specs/](specs/) | Feature and product specs | What we're building and why. A spec precedes non-trivial features. **Feature-level decisions** (the grill outcome + rejected alternatives) live in the spec's own **"Decision record"** section, not as a separate ADR. |
| [tech-debt/](tech-debt/) | Known, deliberate shortcuts | Every entry says what the debt is, why we took it, and what triggers paying it down. |
| [design/](design/) | Claude design exports (self-contained HTML) | The visual reference for implementation. Read these before building UI. |
| [ops/](ops/) | Production reference | Where things actually live (domain, hosting, deploy flow) and a debugging quick-reference. Kept up to date as production changes — not an ADR (it's facts, not a decision) and not tech-debt (it's not a shortcut). |

## The workflow

1. Big or irreversible decision coming up? Run `/grill <topic>` — it researches
   current docs first when external tech is involved, then grills.
2. A grill session that ends in a decision records it where it belongs:
   - **Cross-cutting architecture/infra** → a new ADR from [adr/template.md](adr/template.md).
   - **A feature-level decision** → a **"Decision record"** section in that feature's spec
     (grill outcome + rejected alternatives), not a new ADR.
3. Claude may also *propose* a grill mid-task when a decision smells load-bearing — the
   human approves before anything is written.
4. Deliberate shortcuts get a tech-debt entry at the moment they're taken, not later.

## What does NOT get an ADR

Library bumps, naming, formatting, folder moves, anything easily reversed — if it can be
undone in an afternoon, it's not an ADR. **Feature decisions don't get one either**: they
live in the feature spec's "Decision record" section. ADRs are reserved for the small set
of cross-cutting architecture/infra choices (the stack, API contract, data model,
deployment, auth).
