# ADR-0027: Installable home-screen PWA — manifest-only, no service worker

- **Status:** accepted
- **Date:** 2026-07-18

## Context

We want users to launch Settl from their phone home screen like a native app;
the real question is how much PWA machinery that requires. Research (July 2026):
Chrome dropped the service-worker requirement for menu-based install (mobile v108
/ desktop v112), so a valid manifest alone makes a site installable. iOS Safari
still has no `beforeinstallprompt` — install is always a manual Share → Add to
Home Screen — and iOS gives a home-screen app a cookie store separate from Safari.
A service worker is the only way to get a custom Android install button or push
notifications, but it introduces app-shell caching and a stale-version update
lifecycle. Settl needs the live API on essentially every screen (all business
logic is server-side, ADR-0006), so offline caching has little value.

## Decision

We will make Settl installable as a **manifest-only PWA, with no service worker**:
a web manifest (`display: standalone`, `theme_color`/`background_color`,
`start_url`/`scope`), a full icon set (192/512 + 512 maskable PNG, 180
`apple-touch-icon`) and `apple-mobile-web-app-*` meta tags, plus a
platform-detecting iOS "Add to Home Screen" instruction sheet (Android defers to
the browser's built-in install affordance). To close the warm-session staleness
gap, the app polls a small build-version marker (on an interval and on regaining
focus) and shows a `sonner` toast offering `location.reload()` when the running
build is outdated. No new runtime dependencies.

## Consequences

- Updates flow like the website: a **cold launch loads fresh** (existing ETag
  revalidation on the API's `wwwroot/index.html`), so **no re-install is ever
  needed**. A warm, never-closed session keeps old client code until reloaded —
  the version-check toast handles that; data itself stays fresh via TanStack Query.
- **No offline support and no push notifications.** Both are deferrable; push is
  the one real future reason to revisit the no-SW stance (reminders/email already
  exist — ADR-0024).
- **No custom Android install button** — Android users rely on Chrome's built-in
  omnibox/menu install affordance.
- iOS users **log in once inside the installed app** (separate cookie jar); the
  persistent 14-day sliding auth cookie (ADR-0011) keeps them signed in thereafter.
- Optional later perf tweak: serve content-hashed `assets/*` with long-lived
  `immutable` `Cache-Control` (unnecessary for correctness).
- **Revisit** if we adopt push or need true offline — either flips the
  service-worker decision and would supersede this ADR.

## Sources

Fetched 2026-07-18:

- Chrome for Developers — [Revisiting Chrome's installability criteria](https://developer.chrome.com/blog/update-install-criteria)
- MDN — [Making PWAs installable](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable)
- vite-plugin-pwa — [Vite 8 support](https://github.com/vite-pwa/vite-plugin-pwa/issues/918) (noted as available; deliberately not adopted)
