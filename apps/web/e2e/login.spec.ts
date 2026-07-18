import { test, expect } from '@playwright/test'
import {
  createHousehold,
  latestDevVerificationUrl,
  registerConfirmedUser,
  relativePath,
  uniqueSuffix,
} from './helpers'

// LOGIN, SIGNUP & EMAIL VERIFICATION (ADR-0005 + the email-verification decision made
// alongside it). Uses a brand-new account per run so it never collides with seeded members
// or other specs. The account menu's accessible name is a stable aria-label (its visible
// text is desktop-only, see components/account-menu.tsx), so the same selector works on
// both projects.
test('unauthenticated visitors are redirected to /login', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByText('Välkommen tillbaka')).toBeVisible()
})

test('signs up, verifies email, logs out, and logs back in', async ({ page }) => {
  const suffix = uniqueSuffix()
  const name = `E2E Person ${suffix}`
  const email = `e2e-${suffix}@example.com`
  const password = 'Password123!'

  await page.goto('/signup')
  await page.getByLabel('Namn').fill(name)
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Skapa konto' }).click()

  // Signed in but unconfirmed — blocked from the app until the emailed link is followed.
  await expect(page).toHaveURL(/\/verify-email$/)
  await expect(page.getByText('Bekräfta din e-postadress')).toBeVisible()

  // Follow THIS account's verification link, scoped by email so a parallel worker's signup
  // can't hand us someone else's token (the dev channel indexes links by recipient).
  const confirmUrl = await latestDevVerificationUrl(page.request, email)
  await page.goto(relativePath(confirmUrl))
  await expect(page.getByText('E-post bekräftad')).toBeVisible()

  // Now lands in the app shell (no household yet, so home auto-opens the create-household
  // sheet). Dismiss it to get back to the shell chrome, including the account menu.
  await page.goto('/')
  await expect(page).toHaveURL('/?sheet=newHousehold')
  await page.keyboard.press('Escape')
  await expect(page.getByRole('button', { name: `Konto: ${name}` })).toBeVisible()

  // Log out via the account menu.
  await page.getByRole('button', { name: `Konto: ${name}` }).click()
  await page.getByRole('menuitem', { name: 'Logga ut' }).click()
  await expect(page).toHaveURL(/\/login$/)

  // Log back in with the same credentials.
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Logga in' }).click()
  await expect(page).toHaveURL('/?sheet=newHousehold')
})

// REGRESSION: a returning user logging in through the form must land on a rendered
// Home, not a skeleton that hangs until a manual reload. The app mounts
// ActiveHouseholdProvider above the router, so GET /households used to fire and 401 on
// the unauthenticated cold load; that errored query wedged Home after login — login's
// cache invalidation refetched it but `isPending` never flipped back to true, so Home
// either span on the skeleton forever or flashed the "no household" create sheet before
// the data arrived. The fix gates the query on auth (see useHouseholds), so no fetch
// happens until after login and it runs pending -> success cleanly.
//
// We provision the account + a household while authenticated, then clear the browser's
// cookies so the app loads GENUINELY signed out — reproducing the pre-login state a
// returning user (expired/cleared session) hits before logging back in.
test('logging in via the form renders Home without a manual reload', async ({ page }) => {
  const suffix = uniqueSuffix()
  const email = `e2e-relogin-${suffix}@example.com`
  const password = 'Password123!'
  await registerConfirmedUser(page, { name: `E2E Återinlogg ${suffix}`, email, password })
  const householdName = `E2E Hem ${suffix}`
  await createHousehold(page.request, householdName)

  // Drop the session so the next load is unauthenticated — this is where /households 401s.
  await page.context().clearCookies()
  await page.goto('/')
  await expect(page).toHaveURL(/\/login$/)

  // A returning user spends a moment on the login screen before submitting. That pause is
  // what made the bug deterministic: the unauthenticated /households query has time to fully
  // settle (exhaust retries) before login, so login's cache invalidation refetches an
  // already-errored query whose `isPending` never flips back — Home then hung on the skeleton
  // or flashed the "no household" create sheet. Log in too fast and the query is still
  // in-flight and happens to recover, hiding the regression. The gate on auth removes the
  // pre-login fetch entirely, so this timing no longer matters.
  await page.waitForTimeout(2500)

  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Logga in' }).click()

  // The single-book overview must render straight away — no reload.
  await expect(page.getByRole('heading', { name: 'Ditt hushåll', level: 1 })).toBeVisible()
  await expect(
    page.getByTestId('household-card').filter({ hasText: householdName }),
  ).toBeVisible()
  // The loading skeleton (aria-busy) must be gone, not stuck.
  await expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
})

test('shows an inline error for the wrong password', async ({ page }) => {
  const suffix = uniqueSuffix()
  const email = `e2e-${suffix}@example.com`

  await page.goto('/signup')
  await page.getByLabel('Namn').fill(`E2E Fel ${suffix}`)
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill('CorrectPassword123!')
  await page.getByRole('button', { name: 'Skapa konto' }).click()
  await expect(page).toHaveURL(/\/verify-email$/)

  // Log out via the pending-verification screen's own logout button — no need to verify
  // the email just to exercise the login form's wrong-password error.
  await page.getByRole('button', { name: 'Logga ut' }).click()
  await expect(page).toHaveURL(/\/login$/)

  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill('WrongPassword!')
  await page.getByRole('button', { name: 'Logga in' }).click()

  await expect(page.getByText('Fel e-post eller lösenord.')).toBeVisible()
  await expect(page).toHaveURL(/\/login$/)
})
