# 0011: Client-side QR library for desktop Swish pay codes

**What:** [Swish settlement payments](../specs/swish-settlement-payments.md) renders a QR on
desktop so a debtor can scan the pre-fill link with their phone. The chosen path adds a small
**client-side QR-generation dependency** to `apps/web` (e.g. `qrcode`) rather than calling
Swish's free pre-fill QR image endpoint (`mpc.getswish.net/qrg-swish/api/v1/prefilled`). This is
a new runtime dependency introduced solely for one presentational widget, and it partly bends
ADR-0006 ("UIs render, never compute") — the QR *encoding* of the API-built URL happens in the
browser. The API stays authoritative for the URL itself; only the pixel encoding is client-side.
Recorded pending implementation; nothing is added to `package.json` until sign-off (root rule:
no new dependency without stating why first).

**Why we took it:** The alternative — Swish's hosted QR image endpoint — trades the dependency
for an **external network call on every render**, adding latency, a third-party availability
dependency, and a data-egress path (the pre-fill URL, which embeds the creditor's Swish number,
would be sent to getswish per view). A vetted, tiny, offline QR library keeps rendering local,
fast, and private, with no new backend surface. QR pixel-encoding is pure and deterministic, so
doing it in the browser doesn't put any *business* logic (amount, number, message) in the UI —
those are still computed and returned by the API.

**Trigger to pay it down:** Drop the dependency if a shared QR need appears elsewhere and moves
QR rendering server-side (API returns an SVG/PNG), or if the chosen library goes unmaintained or
pulls in transitive bloat — at which point reconsider the hosted Swish endpoint or a server-side
generator. Re-evaluate as part of any future Swish Commerce API work, which would replace the
self-generated pre-fill URL with token-based QR flows anyway.
