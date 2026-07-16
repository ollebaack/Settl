---
name: grill
description: Interrogate a decision before committing to it. Use when the user says "grill me", "/grill <topic>", or faces an important/irreversible choice. Ends in a decision and the right artifact — an ADR, a spec, or just a summary.
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
   - Recommend one of the answers: make it the first option and append " (Recommended)"
     to its label, so the user gets a clear steer rather than a neutral menu. Recommend
     the option you actually think is best on the evidence, not the user's default.
3. **Stop when marginal questions stop changing the outcome.** Two rounds is usually
   enough; don't pad.
4. **Close with a decision summary:** what was decided, what was explicitly rejected
   and why, and consequences accepted.

## Output

Pick the artifact that fits the outcome — not every grill is an ADR.

- **ADR** — when the outcome is a *decision*: expensive to reverse, shapes
  architecture, or fixes a product principle. Write ONE ADR using
  [docs/adr/template.md](../../../docs/adr/template.md), next number in sequence.
  Keep it under a page — short and decision-shaped.
- **Spec** — when the outcome is *what we're building and why* (a feature's shape,
  scope, or behaviour) rather than a hard-to-reverse decision. Write or update a file
  in [docs/specs/](../../../docs/specs/), one per feature, from
  [docs/specs/template.md](../../../docs/specs/template.md) (see
  [docs/specs/README.md](../../../docs/specs/README.md)). A grill can produce a spec
  and reference a separate ADR for a load-bearing decision inside it — don't force
  feature scope into ADR shape.
- **Neither** — if it's neither decision- nor spec-worthy, say so explicitly and just
  record the summary in the conversation. Not every grill produces a doc; that's the point.
- If a deliberate shortcut was accepted, add a `docs/tech-debt/` entry now (in
  addition to any of the above).

When more than one could apply, ask the user which artifact they want before writing.

## Rules

- Never write any artifact (ADR or spec) before the user has confirmed the final position.
- One ADR per decision, not per session.
- If the user's answer reveals a missing prerequisite decision, name it and offer to
  grill that instead.
