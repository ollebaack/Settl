---
name: ui-design
description: Design or change UI in Settl — iterate screens in Claude Design, verify the render, then mirror the export into docs/design/. Use for any new screen, visual change, redesign, or design spec for apps/web, and whenever a grill/ADR produces UI that needs designing.
---

# Design (Claude Design → docs/design/)

`docs/design/` holds self-contained Claude Design exports (`*.dc.html`) — the **visual
reference read before building UI** (`docs/README.md`). This skill is the loop that
keeps it current: design in Claude Design, verify it renders, mirror it into
`docs/design/`. UIs render and call; they never compute (ADR-0006) — design accordingly.

## When to use

- A new screen or flow needs designing (often right after a `/grill` fixes the product
  decision — build to the ADR, don't redesign it).
- An existing screen changes visually, or a design issue needs fixing.
- Someone asks to "update `docs/design/` from Claude Design."

## Workflow

1. **Orient — learn the visual system first.** Read [`docs/design/Settl App.dc.html`](../../../docs/design/Settl%20App.dc.html)
   (the main prototype) and skim `docs/design/` for prior exports and `*-addendum.md`
   files. Match the existing vocabulary: copy tone (Swedish UI), palette, card/sheet
   radii, hover/active states, density. Never invent new colors — use the tokens below.

2. **Design in Claude Design** (MCP `mcp__claude_design__*`; load schemas via ToolSearch).
   - Project: **"Settl household debt tracker"** — `list_projects` to get its id; don't
     create a new project for app UI.
   - Call `get_claude_design_prompt` **before** any `write_files` (required; teaches the
     `.dc.html` / Design Components format).
   - **New screens get their own `.dc.html` file** (precedent: `Settl Sign In.dc.html`,
     `Settl Household Management.dc.html`), not surgical edits to the 1000-line app file.
     Reuse the shared `./support.js` runtime already at the project/`docs/design/` root.
   - Optional: `put_conversation` to seed the design brief as a chat in the project.
   - Thread `etags` (`if_match`) through every write so a concurrent edit is caught.

3. **Verify the render — don't ship blind.** After writing, `render_preview` and
   screenshot; gate on console errors, 404s, or a blank mount before judging design.
   **Local fallback** (works even without the online project): the [`.claude/launch.json`](../../../.claude/launch.json)
   `design-preview` config serves `docs/design/` over `python -m http.server` — start it
   with `preview_start`, navigate to the `.dc.html`, screenshot, and confirm the DC
   runtime mounted (helmet `<style>` reached `<head>`, theme tokens resolved).

4. **Mirror into `docs/design/`.** Copy the verified `.dc.html` into `docs/design/`
   (plus `support.js` only if it's a new directory). When adding new screens, also write
   a short `<feature>-addendum.md` mapping each screen to its **proposed** API contract,
   matching the existing addenda (`auth-onboarding-addendum.md`, `category-icons-addendum.md`).

5. **Close — flag the follow-ups.** An API-shape change the design implies → regenerate
   `packages/api-client` in the implementing PR (root rule). An important/irreversible
   decision surfaced while designing → `/grill` it before building (ADRs only from grills).

## Design system reference

- **Fonts:** `Bricolage Grotesque` (UI), `Spline Sans Mono` (amounts/money).
- **Tokens** (mint theme; also `paper`/`citrus` + `*-dark`, set via `data-theme`):
  `--bg #f4f6f2 · --card #fff · --ink #1c2620 · --sub #64715f · --line #e2e7dd ·
  --accent #2e7d5b · --soft #e2f0e6 · --pos #2e7d5b · --neg #bb5433 · --chip #eaeee6`.
- **Money:** integer minor units, `sv-SE` formatting, `kr` suffix, mono font.
- **Avatars:** letter-initial on a per-member color — **no photos**. Distinguish a
  **household** (rounded square) from a **person** (circle) so the bubbles don't blend.
- **Mobile-first:** the app is used mostly on phones; hit targets ≥ 44px.

## Gotchas

- **`needs_project_grant`**: writing to the online Claude Design project needs a one-time
  edit grant that can't be approved in a non-interactive session. If it blocks, author
  the verified `.dc.html` straight into `docs/design/` (the real deliverable) and tell the
  user to approve the grant at claude.ai/design/settings to mirror it online.
- Never put a `serve_url` (`*.claudeusercontent.com`) in user-facing text — it carries a
  short-lived token. Share only `claude.ai/design/...` links.
- `docs/design/` also contains authored-only files (`api-contract.md`,
  `implementation-map.md`) that are **not** in the Claude Design project — never delete
  them when syncing; only add/update exports.
