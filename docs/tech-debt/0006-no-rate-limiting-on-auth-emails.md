# 0006: No rate limiting on verification/password-reset email sends

**What:** `POST /auth/resend-verification` and `POST /auth/forgot-password` send an email
every time they're called, with no throttling. An unconfirmed account holder (or anyone
who knows a registered email) can trigger unlimited resend/reset emails to the same inbox.

**Why we took it:** ADR-0011 already accepted the equivalent risk for open signup itself
("rate-limiting/spam is our problem"). These two endpoints are the same shape of risk at
near-zero volume — not worth building a rate limiter for before it's ever been a problem.
`forgot-password` doesn't leak whether an email is registered (same 204 either way), so the
only cost of abuse is inbox noise, not data exposure.

**Trigger to pay it down:** Real signup volume, or a support/abuse report of someone being
spammed via these endpoints. Add per-email/per-IP rate limiting (ASP.NET Core's built-in
`Microsoft.AspNetCore.RateLimiting` middleware) to both endpoints at that point.
