# Mobile app (native iOS) — spec

We're building a native iOS app (`apps/mobile`) as the long-promised native surface for
Settl (product brief; the Expo goal was fixed in [ADR-0002](../adr/0002-frontend-react-vite-spa.md)).
The load-bearing platform decision and its rejected alternatives are in the
[Decision record](#decision-record) below. The "why native at all": App Store presence,
native UX polish a WebView can't match, and — eventually — EU-reliable push that the installed
PWA ([installable-pwa](installable-pwa.md)) can't guarantee under the DMA. The native client's
auth scheme is fixed in [ADR-0005](../adr/0005-auth-aspnet-identity.md), not here.

Provenance: decided via `/grill` on 2026-07-18.

## Problem

Settl is web-only. The installable PWA ([installable-pwa](installable-pwa.md)) gets an icon on
the home screen but can't offer a real App Store listing, native-quality interaction, or (in the
EU) dependable push. Reminders are email-only today ([reminder-delivery](reminder-delivery.md)).
There is no `apps/mobile`.

## Scope (v1 — thin vertical slice)

The first shippable TestFlight build proves the whole pipeline end-to-end, not the whole product:

- **Auth** — direct email/password sign-in, tokens in the iOS Keychain, stay signed in (opaque
  bearer tokens, [ADR-0005](../adr/0005-auth-aspnet-identity.md)). No in-app registration,
  invite-accept, or password-reset in v1 — those stay web-only.
- **View one household's ledger** — net balance up top, itemized history underneath (mirrors the
  web's two-level display, product brief). Read-only.
- **No entry creation, editing, splitting, settlement, or multi-household switching** in v1 —
  those follow once the pipeline is proven.
- **Ships via EAS Build → TestFlight** (no local Xcode; we're on Windows).

Reuses the existing API and `packages/api-client` types verbatim — the app renders and calls,
never computes ([ADR-0006](../adr/0006-api-first-contract.md)).

## API surface

No new business endpoints for v1; the app consumes existing ledger/household reads. The one API
change is the **native bearer-auth path** — the Identity `BearerToken` handler (`AddBearerToken`)
alongside the existing cookie scheme, plus two endpoints, `POST /auth/token` and
`POST /auth/token/refresh`, that mirror the cookie login (details in
[ADR-0005](../adr/0005-auth-aspnet-identity.md)). Shipped in the same change as this spec; the
regenerated `packages/api-client` carries both endpoints (root rule).

## Mobile surface

- `apps/mobile` — Expo app in the pnpm workspace; NativeWind + React Native Reusables base
  components, Expo UI for OS-feel surfaces. Design is derived from the existing mobile-first web
  screens (no separate design export yet — a later `ui-design` pass can produce native mocks if
  the port needs them).
- Screens: sign-in, household ledger (balance + history). That's it for v1.

## Decision record

Decided via `/grill` on 2026-07-18. Build the iOS app on **Expo / React Native** (scaffolded on
**SDK 57** — the current stable at scaffold time, RN 0.86 / React 19.2, New Architecture),
starting with a **thin vertical slice** rather than full parity. UI is **NativeWind
+ React Native Reusables** (shadcn-on-RN) as the base, reaching for **Expo UI** (SwiftUI
primitives) on OS-feel surfaces. Builds and distribution go through **EAS Build** (cloud macOS) +
TestFlight, since we develop on Windows and have no local Xcode.

**Temporary downgrade to SDK 54 (2026-07-19):** the App Store's Expo Go build was stuck behind
SDK 57 with no ETA, blocking iOS iteration on a real device without paying for Apple Developer
Program enrollment. First tried SDK 56, still incompatible; checked the installed Expo Go app
directly (Profile tab → Client version / Supported SDK), which reported **SDK 54** as the actual
ceiling. Downgraded `apps/mobile` to SDK 54 (RN 0.81) to match. Revert to SDK 57
(`npx expo install expo@57 --fix` in `apps/mobile`, `rm -rf node_modules pnpm-lock.yaml && pnpm
install` at the repo root to force a full re-resolution — incremental `expo install`/`pnpm
dedupe` left stale peer conflicts baked into the lockfile both times) once Expo Go on the App
Store supports 57. Always check the phone's actual Expo Go "Supported SDK" value before assuming
which version to target — Apple's approval lag varies per SDK, and the gap can be more than one
version.

Downgrading to SDK 54 also surfaced a real bug: `expo-image@3.0.11` and `expo-status-bar@3.0.9`
(the versions SDK 54 pins) ship `main` pointing at raw `.ts` source instead of an `app.plugin.js`
entry. Expo CLI's config-plugin resolver `require()`s every entry in `app.json`'s `plugins` array
directly under plain Node, and Node 22's default type-stripping refuses to process `.ts` files
under `node_modules`, crashing `expo config`/`expo-doctor`/the dev server outright instead of
skipping the package gracefully. Neither package needs a plugin entry at this SDK (there's no
`app.plugin.js` to run), so both were removed from `plugins` in `app.json`. Re-add them if
reverting to SDK 57, where both ship real config plugins.

Rejected alternatives:

- **Capacitor (wrap the existing PWA)** — ships to the App Store fast reusing 100% of the web UI,
  but it's a WebView and can't deliver the native UX polish that is a stated driver. Rejected on
  that basis; would have been the cheaper path if polish weren't a goal.
- **Stay PWA + web push** — cheapest, but bets on EU PWA push being reliable, which is exactly the
  uncertain point (DMA). Doesn't get an App Store listing.
- **Tamagui / Expo-UI-everywhere** — a heavier universal system / furthest from the web's Tailwind
  workflow; NativeWind keeps the mental model closest to `apps/web`.

Consequences accepted: the mobile UI is **rebuilt, not reused** (shadcn/DOM components don't carry
— [ADR-0002](../adr/0002-frontend-react-vite-spa.md) anticipated this); WebKit/native divergence
from the web; a second auth scheme in the API ([ADR-0005](../adr/0005-auth-aspnet-identity.md)); a
hard dependency on EAS Build (and its free-tier build limits); single-RN-version discipline across
the pnpm workspace (SDK 54+ isolated installs + automatic Metro config; Windows Developer Mode for
symlinks).

**Revisit** if: the thin slice shows WebView-grade UX would have sufficed (Capacitor was cheaper);
EAS Build cost/limits bite; or push needs direct APNs (Live Activities / Time-Sensitive).

## Out of scope / open questions

- **Push (phase 2)** — Expo Push Service (free, unified iOS+Android, native APNs token retrievable
  so not a lock-in) + a device-token registry wired into the nudge engine
  ([reminder-delivery](reminder-delivery.md)). Confirm the EU-PWA-push-degradation claim while
  scoping it.
- **Android** — Expo is cross-platform, but v1 targets iOS only; Android is a later call
  (per-platform Expo UI files, store setup).
- **Feature parity** — entry add/edit, flexible splits, recurring, settlement, multi-household —
  all deferred until the slice proves the platform plumbing.
- **EAS Build cost/limits** — validate the free tier covers our cadence, or budget a paid plan.

## Sources

Fetched 2026-07-18:

- Expo — [SDK 56 changelog](https://expo.dev/changelog/sdk-56-beta), [New Architecture](https://docs.expo.dev/guides/new-architecture/), [Work with monorepos](https://docs.expo.dev/guides/monorepos/), [EAS Build with a monorepo](https://docs.expo.dev/build-reference/build-with-monorepos/), [Using push notifications services](https://docs.expo.dev/guides/using-push-notifications-services/), [Building SwiftUI apps with Expo UI](https://docs.expo.dev/guides/expo-ui-swift-ui/)
- [React Native Reusables](https://reactnativereusables.com/)
- Apple — [Sending web push notifications in web apps and browsers](https://developer.apple.com/documentation/usernotifications/sending-web-push-notifications-in-web-apps-and-browsers); MagicBell — [PWA iOS limitations 2026](https://www.magicbell.com/blog/pwa-ios-limitations-safari-support-complete-guide)
- Capacitor — [Using Capacitor with React](https://capacitorjs.com/solution/react) (evaluated, not adopted)
