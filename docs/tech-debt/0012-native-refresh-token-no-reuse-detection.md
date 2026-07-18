# 0012: Native refresh tokens have no reuse-detection

**What:** The native client authenticates with opaque Identity `BearerToken`s
(ADR-0005). Our `POST /auth/token/refresh` re-checks the Identity security stamp on
every call — so logout and password-change revoke all sessions — but it does **not**
detect a *replayed* refresh token. If a refresh token is stolen and used by an attacker,
the flow won't notice the reuse and revoke the whole token family the way 2026 native
best practice (rotation + reuse-detection → revoke-all) prescribes.

**Why we took it:** The refresh token lives in the iOS Keychain via `expo-secure-store`
(OS-level encryption), and Settl is a single first-party client, so the theft surface is
small. Security-stamp revalidation already covers the revocation paths that matter most
(logout, password change). True reuse-detection means a server-side refresh-token store
with one-time-use bookkeeping — not worth it before the app has real users.

**Trigger to pay it down:** A credible refresh-token-theft threat, an audit/compliance
requirement, or the point where we move to a custom-signed-JWT scheme for other reasons
(ADR-0005). At that point, add a refresh-token table with one-time-use rotation and
reuse-detection that revokes the whole token family on replay.
