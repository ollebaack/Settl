# Feature name — spec

One paragraph: what we're building and why, in plain terms. Link the load-bearing
references — visual design (`docs/design/…`) and any decision this rests on
([ADR-NNNN](../adr/NNNN-slug.md)). Keep the "why" here; the "how" goes below.

Provenance: how this spec came to be — e.g. "Decided via `/grill` on YYYY-MM-DD."
If a grill produced it, note whether a companion ADR exists or why the decision
wasn't ADR-worthy.

## Problem

2–4 sentences. What's wrong or missing today? Point at the concrete code or behaviour
this replaces (`path/to/file.ts:NN`) so the gap is unambiguous.

## Scope (v1)

The bullet list of what this feature does. Be specific about boundaries — what's
included, and the deliberate "no X here" calls that keep it small. Reference existing
paths/endpoints you're reusing rather than restating them.

## Data model

Only if the feature touches persistence. New tables/columns/enums, defaults, and the
migration/backfill plan. Delete this section if it doesn't apply.

## API surface

New or changed endpoints, DTOs, and requests. Note the api-client regeneration if the
shape changes (root rule). Delete if not applicable.

## Web surface

New or changed screens/components and how they wire to the API. Reference the design
export the UI is built from. Delete if not applicable.

## Out of scope / open questions

What this spec explicitly does NOT cover, and the questions still unresolved. Open
questions are grill fodder — a later grill session resolves them and updates this spec.
