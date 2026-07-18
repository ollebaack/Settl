# Mobile app rules (native iOS)

Native iOS client — Expo / React Native. Spec: `docs/specs/mobile-app.md`. Platform
decision lives in that spec's Decision record; auth in ADR-0005.

- **Expo SDK 57 is pinned.** Expo changes fast — check the versioned docs at
  https://docs.expo.dev/versions/v57.0.0/ before writing native code. Add native modules
  with `pnpm exec expo install <pkg>` (resolves the SDK-correct version), never a bare
  `pnpm add`.
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
