import { defineConfig, devices } from '@playwright/test'

// E2E runs against the real stack: the .NET API and the Vite dev server. Locally, running
// servers are reused; otherwise both are booted.
//
// Postgres: locally, each run gets its OWN throwaway container (api-with-postgres.mjs boots
// it, waits, then starts the API against it, and removes it on exit) — so parallel worktrees
// never share DB state or fight over a port. CI instead uses its `services: postgres`
// container (E2E_DB below routes around the throwaway path); set E2E_DB manually to point at
// any existing database and the container is likewise skipped.
//
// Env overrides so the suite can run in isolation alongside another worktree's stack:
//   E2E_API_URL   API origin            (default http://localhost:5000)
//   E2E_WEB_URL   Vite origin           (default http://localhost:5173)
//   E2E_DB        API connection string (default: throwaway container locally, :5432 in CI)
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5000'
const WEB_URL = process.env.E2E_WEB_URL ?? 'http://localhost:5173'
const WEB_PORT = new URL(WEB_URL).port || '5173'

// A per-run id shared with api-with-postgres.mjs (via webServer env) and global-teardown.mjs
// (via process.env) so container cleanup is scoped to this run only.
const RUN_ID = process.env.E2E_RUN_ID ?? `${Date.now()}-${process.pid}`
process.env.E2E_RUN_ID = RUN_ID

// When set, the API talks to this DB directly and no throwaway container is created. CI has
// no explicit E2E_DB but does provide a `services: postgres` on :5432, so route CI there.
const CI_DB = 'Host=localhost;Port=5432;Database=e2e;Username=postgres;Password=postgres'
const explicitDb = process.env.E2E_DB ?? (process.env.CI ? CI_DB : undefined)

export default defineConfig({
  testDir: './e2e',
  globalTeardown: './e2e/global-teardown.mjs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: WEB_URL,
    trace: 'on-first-retry',
  },
  projects: [
    // Mobile-first product: primary viewport is a phone; desktop is the scale-up check.
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: [
    {
      command: 'node e2e/api-with-postgres.mjs',
      url: `${API_URL}/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        E2E_API_URL: API_URL,
        E2E_RUN_ID: RUN_ID,
        ...(explicitDb ? { E2E_DB: explicitDb } : {}),
      },
    },
    {
      command: `pnpm dev --port ${WEB_PORT}`,
      url: WEB_URL,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        VITE_API_BASE_URL: API_URL,
      },
    },
  ],
})
