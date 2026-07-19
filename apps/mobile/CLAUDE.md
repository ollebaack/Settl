# Mobile app rules (native iOS)

Native iOS client — Expo / React Native. Spec: `docs/specs/mobile-app.md`. Platform
decision lives in that spec's Decision record; auth in ADR-0005.

- **Expo SDK 54 is pinned (temporarily — see `docs/specs/mobile-app.md`'s decision record).**
  Downgraded from 57 on 2026-07-19 to match what the installed Expo Go app on the App Store
  actually supports (checked via the app's own Profile tab — the App Store lagged by more than
  one SDK version). Check the versioned docs at https://docs.expo.dev/versions/v54.0.0/ before
  writing native code. Add native modules with `pnpm exec expo install <pkg>` (resolves the
  SDK-correct version), never a bare `pnpm add`. Revert to 57 once Expo Go on the App Store
  supports it (see the spec for the revert command) — and re-check the phone's reported
  "Supported SDK" rather than assuming.
- `expo-image` and `expo-status-bar` are deliberately **absent from `app.json`'s `plugins`
  array** at SDK 54 — both ship a raw `.ts` `main` at this version instead of `app.plugin.js`,
  which crashes Expo CLI's config resolution under Node 22's type-stripping. Re-add them if/when
  reverting to SDK 57.
- **Render and call, never compute** (ADR-0006). Business logic is server-side; consume
  `@settl/api-client` types and the API. Money = integer minor units; timestamps UTC.
- **Styling is NativeWind** (`className`, Tailwind v3 config). Reach for `@expo/ui`
  (SwiftUI primitives) on OS-feel surfaces. Add shared components via React Native
  Reusables the way the web adds shadcn — copy-in, not a runtime framework.
- **Auth = opaque bearer tokens in `expo-secure-store`** (Keychain), refreshed via
  `/auth/token/refresh` (ADR-0005). Never persist tokens in AsyncStorage.
- **One React Native version across the workspace.** Metro uses Expo's default config
  (built-in pnpm-monorepo support) — don't hand-roll `watchFolders`.
- **Builds run on EAS** (cloud macOS) — no local Xcode. `eas.json` stays in this dir.
