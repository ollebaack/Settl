# @settl/mobile

Native iOS client (Expo / React Native, SDK 54 — temporarily downgraded from 57, see the spec's
decision record). Spec: [`docs/specs/mobile-app.md`](../../docs/specs/mobile-app.md);
agent rules: [`CLAUDE.md`](./CLAUDE.md).

## Local dev

```bash
pnpm --filter @settl/mobile start        # Metro; press i for iOS simulator (macOS only)
pnpm --filter @settl/mobile typecheck    # tsc --noEmit (part of root `pnpm verify`)
```

Point the app at an API with `EXPO_PUBLIC_API_URL` — `localhost` resolves from the iOS
simulator on the same Mac, but **a physical device needs the dev machine's LAN IP** (or a
tunnel):

```bash
EXPO_PUBLIC_API_URL=http://192.168.1.20:5000 pnpm --filter @settl/mobile start
```

## Building & shipping (EAS)

Builds run on **EAS** (cloud macOS) — there is no local Xcode on Windows. `eas.json` holds
the profiles; convenience scripts wrap the commands. `eas` is not a project dependency
(it's large and rarely needed in CI) — install it globally once:

```bash
npm install -g eas-cli      # or: pnpm add -g eas-cli
```

### One-time account setup (interactive — run these yourself)

These need an **Expo account** and, for anything beyond the simulator profile, an **Apple
Developer Program** membership ($99/yr). They're interactive (browser/keychain prompts), so
they can't run in an automated session.

```bash
cd apps/mobile
eas login                   # Expo account
eas init                    # creates the EAS project; writes extra.eas.projectId to app.json
eas credentials             # (for device/TestFlight) generate the iOS distribution cert + APNs key on Apple's servers
```

`eas init` is the step that unblocks builds — until it writes a `projectId`, `eas build`
will refuse to run.

### Validate the pipeline for free (no Apple account)

The `simulator` profile produces a `.app` for the iOS Simulator and needs **no Apple
credentials** — the cheapest way to prove the whole build config works end-to-end:

```bash
pnpm --filter @settl/mobile build:sim
```

### Device / TestFlight

Needs Apple Developer enrollment + `eas credentials` done:

```bash
pnpm --filter @settl/mobile build:preview   # internal (ad-hoc) build for real-device testing
pnpm --filter @settl/mobile build:prod      # store build (autoIncrement)
pnpm --filter @settl/mobile submit:ios      # upload the latest prod build to App Store Connect → TestFlight
```

## Where things are

- `src/app/` — expo-router screens (`sign-in`, `index` = households, `household/[id]` = ledger)
- `src/lib/` — API client + bearer/refresh (`api.ts`), keychain tokens (`tokens.ts`), auth
  context (`auth.tsx`), formatting/labels, generated types (`types.ts`)
