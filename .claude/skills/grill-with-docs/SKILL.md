---
name: grill-with-docs
description: Research current documentation first, then grill the decision with evidence. Use for choices involving external tech (libraries, frameworks, services, APIs) where training data may be stale — "/grill-with-docs <topic>".
---

# Grill with docs

Same contract as the `grill` skill, but the grilling is grounded in current, primary
sources instead of opinion. Use this when the decision involves external technology
where versions, pricing, or APIs change.

## Process

1. **Frame the decision** in one sentence and identify what facts would change the
   answer (version support, pricing, maintenance status, migration paths, limits).
2. **Research before asking anything.** Use WebSearch/WebFetch on primary sources:
   official docs, changelogs, GitHub issues/releases. Prefer pages dated within the
   last year. Your knowledge cutoff means anything could have changed — verify, don't
   recall. 3–6 sources is typical; note anything that contradicts your priors.
3. **Grill in rounds** (AskUserQuestion, max 4/round) — but now every option cites
   evidence: "X dropped Y in v3 (changelog, 2026-01)". If research already settles a
   question, don't ask it; state the finding. Recommend one of the answers: make it the
   first option and append " (Recommended)" to its label. Base the recommendation on the
   evidence you fetched, not on priors.
4. **Close** exactly like `grill`: decision summary → the right artifact (ADR, spec,
   or just a summary) per `grill`'s Output rules. When it's an ADR, include a short
   "Sources" list of load-bearing links in its Context; a spec grounded in research
   should cite the same way. Add a tech-debt entry for accepted shortcuts.

## Rules

- No recommendation without a source you actually fetched this session.
- If sources conflict or are stale, say so — uncertainty is a finding.
- Everything in `grill`'s Rules applies here too.
