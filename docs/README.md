# Settl docs

How knowledge is organized in this repo:

| Folder | What lives here | Rule |
| --- | --- | --- |
| [adr/](adr/) | Architecture Decision Records | Short. Important decisions only. Written as the outcome of a grill session (`/grill` or `/grill-with-docs`), never casually. |
| [specs/](specs/) | Feature and product specs | What we're building and why. A spec precedes non-trivial features. |
| [tech-debt/](tech-debt/) | Known, deliberate shortcuts | Every entry says what the debt is, why we took it, and what triggers paying it down. |
| [design/](design/) | Claude design exports (self-contained HTML) | The visual reference for implementation. Read these before building UI. |

## The workflow

1. Big or irreversible decision coming up? Run `/grill <topic>` (opinion-based) or
   `/grill-with-docs <topic>` (researches current docs first, then grills).
2. A grill session that ends in a decision produces one ADR from [adr/template.md](adr/template.md).
3. Claude may also *propose* a grill mid-task when a decision smells ADR-worthy — the
   human approves before anything is written.
4. Deliberate shortcuts get a tech-debt entry at the moment they're taken, not later.

## What does NOT get an ADR

Library bumps, naming, formatting, folder moves, anything easily reversed.
If it can be undone in an afternoon, it's not an ADR.
