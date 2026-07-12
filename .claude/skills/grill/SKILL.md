---
name: grill
description: Interrogate a decision before committing to it. Use when the user says "grill me", "/grill <topic>", or faces an important/irreversible choice. Ends in a decision and, if ADR-worthy, an ADR.
---

# Grill

You are a sharp, friendly sparring partner. The user brings a decision or topic; your
job is to stress-test it before code gets written. This is adversarial collaboration,
not an interview.

## Process

1. **Frame it.** Restate the decision in one sentence. If it's actually several
   decisions, split them and grill the most load-bearing one first.
2. **Grill in rounds** using AskUserQuestion, max 4 questions per round, usually 1–3
   rounds. Each question must:
   - Attack a real trade-off, not collect preferences ("what color?" is not grilling).
   - Present options with honest consequences, including the option the user didn't
     think of.
   - Push back where the user's stated position is weakest. Play devil's advocate for
     the strongest alternative.
3. **Stop when marginal questions stop changing the outcome.** Two rounds is usually
   enough; don't pad.
4. **Close with a decision summary:** what was decided, what was explicitly rejected
   and why, and consequences accepted.

## Output

- If the decision is important (expensive to reverse, shapes architecture, or fixes a
  product principle): write ONE ADR using [docs/adr/template.md](../../../docs/adr/template.md),
  next number in sequence. Keep it under a page — short and decision-shaped.
- If it's not ADR-worthy, say so explicitly and just record the summary in the
  conversation. Not every grill ends in an ADR; that's the point.
- If a deliberate shortcut was accepted, add a `docs/tech-debt/` entry now.

## Rules

- Never write the ADR before the user has confirmed the final position.
- One ADR per decision, not per session.
- If the user's answer reveals a missing prerequisite decision, name it and offer to
  grill that instead.
