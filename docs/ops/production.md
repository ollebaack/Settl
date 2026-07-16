# Production reference

Where things actually live and how to debug them. Decisions and their rationale are in
the ADRs linked below — this is the "where do I look" companion, kept up to date as
production changes.

## Topology

- **Domain:** `settlapp.se`, registered at Loopia, nameservers on Cloudflare.
- **DNS/edge:** Cloudflare (dash.cloudflare.com). SSL/TLS mode must be **Full (strict)**
  under SSL/TLS → Overview — Cloudflare's "Automatic" mode has been observed to silently
  settle on plain "Full" instead, which skips origin cert validation. If mixed cert
  errors or unexpected plaintext traffic show up, check this setting first.
- **VPS:** Strato VC2-4 (Ubuntu 24.04, 2 vCPU/4GB RAM, IP `31.70.90.93`), running
  [Dokploy](http://31.70.90.93:3000) — see ADR-0014 for why Dokploy instead of a hand-run
  Docker Compose stack.
- **Dokploy layout:** project `settl` (separate from the unrelated `TwoEat` project
  already on this VPS — don't touch that one) → services `settl-postgres` (native
  Dokploy database) and `settl-api` (the app, pulls a prebuilt image, doesn't build
  from Git).
- **Image:** `ghcr.io/ollebaack/settl-api:latest`, built by `.github/workflows/cd.yml`
  after every green CI run on `main`. Public GHCR package — no registry credentials
  needed in Dokploy.

## Deploy flow

1. Push to `main` → CI runs → CD builds the image and pushes it to GHCR, tagged both
   `latest` and `sha-<full-sha>` (`docker/metadata-action` in `cd.yml`).
2. CD's final "Trigger Dokploy deploy" step then calls Dokploy's authenticated deploy
   API (`POST http://31.70.90.93:3000/api/application.deploy` with an `x-api-key` header
   and `{"applicationId": ...}` body) to roll the new image out automatically. We use the
   API-token method rather than the no-auth deploy webhook the Dokploy UI surfaces
   (`settl-api` → Deployments tab): that webhook URL form has a history of 404ing across
   Dokploy versions, whereas the API endpoint is documented and stable.
3. This needs two GitHub **repository secrets** (Settings → Secrets and variables →
   Actions):
   - `DOKPLOY_API_KEY` — a Dokploy API token (Dokploy → profile → `/settings/profile`,
     API/CLI section → generate). Account-scoped, so keep it secret.
   - `DOKPLOY_APP_ID` — the `settl-api` application's `applicationId`. Not sensitive, but
     kept as a secret for uniformity. Find it with the API key:
     `curl -s http://31.70.90.93:3000/api/project.all -H "x-api-key: <token>"` and read
     the `applicationId` under project `settl` → application `settl-api`.
4. If a deploy ever looks like it didn't pick up the new image, open the deployment's log
   in Dokploy and check for `Digest: sha256:...` — a Docker-provider app pulling `:latest`
   can occasionally reuse a cached "up to date" pull. You can always still redeploy by
   hand via the **Deploy** button in the `settl-api` app. To **roll back**, point the
   app's Docker image at a specific `ghcr.io/ollebaack/settl-api:sha-<full-sha>` tag
   (General tab → Provider) instead of `latest`, so you pin an exact prior build rather
   than whatever `latest` currently resolves to, then Deploy.
5. **Before deploying a newer image, confirm `Web__BaseUrl=https://settlapp.se` is set**
   in the Environment tab (see env vars below) — the app fail-fast-crashes on startup if
   it's unset (commit `70f7dfd`), so a deploy against a missing value crash-loops the
   container.

## Environment variables (`settl-api`, Dokploy → Environment tab)

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__Settl` — Npgsql connection string pointing at the `settl-postgres`
  service's internal Dokploy hostname (not localhost, not the public IP).
- `Resend__ApiKey` — real Resend key. No `Resend__FromAddress` override needed; the
  code's built-in default (`no-reply@settlapp.se`) matches the verified domain.
- `Web__BaseUrl=https://settlapp.se` — origin baked into the links in outbound emails
  (email confirmation, password reset) and household invites. **Required in prod:** the
  code falls back to `http://localhost:5173` when unset (`AuthEndpoints.cs`,
  `InvitesEndpoints.cs`), so a missing value ships emails whose links point at the
  recipient's own machine instead of the app.
- `DataProtection__KeyPath=/keys` — directory the ASP.NET Core Data Protection key ring
  is persisted to, backed by the `settl-api-dataprotection-keys` volume mounted at `/keys`
  (Advanced → Volumes). **Required in prod:** without it the key ring is regenerated in
  memory on every redeploy, silently invalidating all auth cookies and every outstanding
  email-confirmation / password-reset token.

## Debugging quick-reference

Several production-only bugs surfaced during initial rollout (none ever showed up in
local dev, since dev's topology differs — see each fix's commit for the full why):

- **Every static asset (JS/CSS) returns 401, only `index.html` loads** → check
  middleware order in `apps/api/Settl.Api/Program.cs`: `UseStaticFiles()` must run
  *before* `UseAuthentication()`/`UseAuthorization()`. `MapFallbackToFile`'s default
  route excludes paths with a file extension, so those requests never match an
  endpoint and fall through to the global auth `FallbackPolicy` if static files hasn't
  already claimed them. Fixed in commit `7b8f9be`.
- **App stuck on a loading skeleton forever, or the browser shows a "wants to access
  other apps and services on this device" (Local Network Access) permission prompt** →
  the production bundle is calling the visitor's own machine instead of the API. Check
  that `apps/api/Settl.Api/Dockerfile` sets `ENV VITE_API_BASE_URL=""` before
  `pnpm --filter web build` — without it, `apps/web/src/lib/api.ts`'s dev-convenience
  fallback (`http://localhost:5000`) gets baked into the production build. Fixed in
  commit `f6b9c78`.
- **Confirmation / password-reset / invite email links point at `localhost:5173`** (so
  clicking one hits the recipient's own machine and the confirm page shows "Länken är
  ogiltig eller har gått ut") → `Web__BaseUrl` is unset; the code falls back to
  `http://localhost:5173` (`AuthEndpoints.cs`, `InvitesEndpoints.cs`). Set it in the
  Environment tab (see env vars above) and redeploy.
- **Login/session dropped or every confirm/reset link fails right after a redeploy** →
  the Data Protection key ring wasn't persisted; check `DataProtection__KeyPath=/keys` is
  set and the `/keys` volume is mounted (see env vars above). Fixed in commit `86bbe6a`.
- **New commit doesn't seem to be live** → CD now auto-triggers the deploy (see "Deploy
  flow" above). Check the CD run's "Trigger Dokploy deploy" step succeeded and that
  Dokploy shows a fresh deployment; if the deployment ran but the image looks stale, it's
  the cached-`:latest`-pull case (check the log's `Digest: sha256:...`), not a code bug.
  A manual **Deploy** in Dokploy is always a safe fallback.
- **Container logs:** Dokploy → `settl-api` → Logs tab, real-time, shows EF Core
  migrations, the `Now listening on` line, and request-level app logs.

## Known gaps

- [tech-debt/0007](../tech-debt/0007-vps-firewall-not-locked-to-cloudflare.md) — VPS
  firewall not yet restricted to Cloudflare's IP ranges.
- [tech-debt/0008](../tech-debt/0008-no-postgres-backups.md) — Postgres backups not yet
  configured (mechanism decided in ADR-0014, not turned on).
