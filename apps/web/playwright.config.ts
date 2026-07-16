import { defineConfig, devices } from '@playwright/test'

// E2E runs against the real stack: the .NET API (isolated "e2e" Postgres database,
// freshly seeded) and the Vite dev server. Locally, running servers are reused; in CI
// both are booted.
//
// Ports + database are env-overridable so the suite can run in isolation
// alongside another worktree that already owns :5000 / the shared `e2e` database:
//   E2E_API_URL   API origin            (default http://localhost:5000)
//   E2E_WEB_URL   Vite origin           (default http://localhost:5173)
//   E2E_DB        API connection string (default the shared `e2e` database)
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5000'
const WEB_URL = process.env.E2E_WEB_URL ?? 'http://localhost:5173'
const DB =
  process.env.E2E_DB ??
  'Host=localhost;Port=5432;Database=e2e;Username=postgres;Password=postgres'
const WEB_PORT = new URL(WEB_URL).port || '5173'

export default defineConfig({
  testDir: './e2e',
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
      command: 'dotnet run --project ../api/Settl.Api --no-launch-profile',
      url: `${API_URL}/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: API_URL,
        ConnectionStrings__Settl: DB,
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
