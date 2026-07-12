# 0001: No email delivery story

**What:** ADR-0005 commits us to self-hosted Identity, which means we own sending
invite links and password-reset mail. No provider chosen, nothing wired.

**Why we took it:** Auth implementation isn't started; choosing an email provider now
would be premature.

**Trigger to pay it down:** The moment household invites are designed. Grill the
provider choice (Resend/Postmark/SES/SMTP) as part of that feature's spec.
