# Installable home-screen PWA — spec

Let users launch Settl from their phone home screen like a native app, with the least PWA
machinery that achieves it: a **manifest-only PWA, no service worker**. The load-bearing
decision and rejected alternatives (ADR-0027, grilled 2026-07-18) are in the
[Decision record](#decision-record-adr-0027) below.

Provenance: decided via `/grill-with-docs` on 2026-07-18 (was ADR-0027). Research (July 2026)
confirmed Chrome dropped the service-worker requirement for menu-based install (mobile v108 /
desktop v112), so a valid manifest alone makes a site installable.

## Problem

Settl is app-like behind a login but can only be opened in a browser tab. Users want a
home-screen launcher. The question is how much PWA machinery that needs — a service worker
brings app-shell caching and a stale-version update lifecycle that Settl, which needs the live
API on essentially every screen (all business logic is server-side, ADR-0006), gains little
from.

## Scope (v1)

- **Manifest-only, no service worker.** A web manifest (`display: standalone`,
  `theme_color`/`background_color`, `start_url`/`scope`), a full icon set (192/512 + 512
  maskable PNG, 180 `apple-touch-icon`) and `apple-mobile-web-app-*` meta tags.
- **Platform-detecting iOS install sheet.** iOS Safari has no `beforeinstallprompt` — install
  is always manual Share → Add to Home Screen — so show a platform-detecting "Add to Home
  Screen" instruction sheet. Android defers to the browser's built-in install affordance.
- **Warm-session update watcher.** The app polls a small build-version marker (on an interval
  and on regaining focus) and shows a `sonner` toast offering `location.reload()` when the
  running build is outdated. **No new runtime dependencies.**

## Web surface

- `vite.config.ts` / `__root.tsx`: manifest + meta tags + icon set.
- `lib/pwa.ts`, `lib/install-prompt.ts`, `components/pwa/install-sheet.tsx`,
  `components/pwa/version-watcher.tsx`, and the account-menu install entry point.

## Decision record (ADR-0027)

Make Settl installable as a **manifest-only PWA, with no service worker**. A service worker is
the only way to get a custom Android install button or push notifications, but it introduces
app-shell caching and a stale-version update lifecycle; Settl needs the live API everywhere, so
offline caching has little value.

Consequences accepted:

- Updates flow like the website — a **cold launch loads fresh** (existing ETag revalidation on
  the API's `wwwroot/index.html`), so **no re-install is ever needed**. A warm, never-closed
  session keeps old client code until reloaded — the version-check toast handles that; data
  stays fresh via TanStack Query.
- **No offline support and no push notifications.** Both deferrable; push is the one real future
  reason to revisit the no-SW stance (reminders already go by email, ADR-0024).
- **No custom Android install button** — Android relies on Chrome's built-in omnibox/menu
  affordance.
- iOS users **log in once inside the installed app** (separate cookie jar); the persistent
  14-day sliding auth cookie (ADR-0005) keeps them signed in thereafter.

**Revisit** if we adopt push or need true offline — either flips the service-worker decision.

## Out of scope / open questions

- Offline support, push notifications, custom Android install button — all deferred (need a
  service worker).
- `vite-plugin-pwa` was noted as available but deliberately not adopted.

## Sources

Fetched 2026-07-18:

- Chrome for Developers — [Revisiting Chrome's installability criteria](https://developer.chrome.com/blog/update-install-criteria)
- MDN — [Making PWAs installable](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable)
- vite-plugin-pwa — [Vite 8 support](https://github.com/vite-pwa/vite-plugin-pwa/issues/918) (available; not adopted)
