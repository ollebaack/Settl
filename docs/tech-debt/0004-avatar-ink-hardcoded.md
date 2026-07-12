# 0004: Member-avatar initial ink uses hardcoded hex

**What:** `apps/web/src/components/member-avatar.tsx` picks a near-black or near-white
color for the member's initial by measuring the luminance of that member's
`avatarColor`, using literal hex (`#1c2620` / `#f4f6f2`). This is the one place a web
component uses hex outside the sanctioned `avatarColor` data carve-out (CLAUDE.md:
semantic tokens only).

**Why we took it:** `avatarColor` is arbitrary per-member data (pastels seeded from the
design), not a theme token, so no semantic token guarantees legible contrast on top of
it. A luminance-picked ink is the simplest correct fix and is theme-independent by
design (the initial must stay readable on the same colored disc in both light and dark).

**Trigger to pay it down:** If avatar colors become themeable/tokenized, or a design
token for "on-arbitrary-surface ink" is introduced, replace the hex literals with it.
Until then this is a deliberate, contained exception.
