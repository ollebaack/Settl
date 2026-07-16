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

1. Push to `main` → CI runs → CD builds the image and pushes it to GHCR.
2. **Nothing auto-redeploys Dokploy.** No webhook is wired from CD to Dokploy's deploy
   webhook (`settl-api` → General tab → Deployments tab shows the per-app webhook URL).
   After CD finishes, someone has to open the `settl-api` app in Dokploy and click
   **Deploy** by hand — check the deployment's log for `Digest: sha256:...` before/after
   to confirm it actually pulled a new image rather than reusing a cached "up to date"
   pull.
3. Wiring the webhook into `cd.yml` as a final step is the obvious next improvement —
   not done yet.

## Environment variables (`settl-api`, Dokploy → Environment tab)

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__Settl` — Npgsql connection string pointing at the `settl-postgres`
  service's internal Dokploy hostname (not localhost, not the public IP).
- `Resend__ApiKey` — real Resend key. No `Resend__FromAddress` override needed; the
  code's built-in default (`no-reply@settlapp.se`) matches the verified domain.

## Debugging quick-reference

Two real production-only bugs surfaced during initial rollout (neither ever showed up in
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
- **New commit doesn't seem to be live** → see "Deploy flow" above; almost certainly
  just needs a manual redeploy in Dokploy, not a real bug.
- **Container logs:** Dokploy → `settl-api` → Logs tab, real-time, shows EF Core
  migrations, the `Now listening on` line, and request-level app logs.

## Known gaps

- [tech-debt/0007](../tech-debt/0007-vps-firewall-not-locked-to-cloudflare.md) — VPS
  firewall not yet restricted to Cloudflare's IP ranges.
- [tech-debt/0008](../tech-debt/0008-no-postgres-backups.md) — Postgres backups not yet
  configured (mechanism decided in ADR-0014, not turned on).
