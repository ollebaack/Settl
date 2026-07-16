# 0010: Profile phone number stored unverified (no OTP)

**What:** ADR-0019 lets a member set their own phone number on their profile, stored
without any SMS OTP proof of ownership. Ownership is proven only indirectly, at
connect time, when an invitee follows the tokenized SMS invite link. A self-entered
profile number is therefore unverified display/contact data.

**Why we took it:** Requiring OTP to save a profile field would couple this feature to
an SMS provider, which ADR-0019 deliberately defers (Resend is email-only; picking a
vendor is its own decision). The blind SMS invite already proves control of a number
at the only moment it matters — when a contact edge is created. At near-zero volume,
an unverified display field is not worth standing up a paid SMS vendor for.

**Trigger to pay it down:** The moment the SMS provider lands (Sinch/Vonage per
ADR-0019), or the first time an unverified profile number needs to be trusted for
anything beyond display (e.g. becoming a lookup key or an auth factor — which ADR-0019
forbids while unverified). Add an SMS OTP challenge on profile-phone save at that point.
Rate-limiting on the SMS invite/OTP path (tech-debt/0006's email equivalent, made
cost-critical by per-message SMS cost) must ship together with the provider.
