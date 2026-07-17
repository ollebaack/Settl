import { test, expect } from '@playwright/test'
import { relativePath, uniqueSuffix } from './helpers'

// Defaults to the standard e2e API; overridable so this spec can also run against an
// isolated stack on another port when the default :5000 is occupied by another worktree.
const API = process.env.E2E_API_URL ?? 'http://localhost:5000'

async function latestVerificationUrl(request: import('@playwright/test').APIRequestContext): Promise<string> {
  const res = await request.get(`${API}/dev/verifications/latest`)
  expect(res.ok(), 'GET /dev/verifications/latest').toBeTruthy()
  return ((await res.json()) as { confirmUrl: string }).confirmUrl
}

// PROFILE — avatar emoji personalization (ADR-0019, profile-addendum §2.1–2.2).
// Each run creates its own freshly-verified account so setting/resetting the emoji never
// races the shared e2e database or other specs. Registering via page.request signs the
// account in on the page's own context, so subsequent page.goto navigations are authed.
// Serial so the three accounts' verification links never bunch up on the shared dev
// "latest verification" side channel.
test.describe.configure({ mode: 'serial' })

async function freshVerifiedAccount(page: import('@playwright/test').Page, request: import('@playwright/test').APIRequestContext) {
  const suffix = uniqueSuffix()
  const name = `Profil ${suffix}`
  const email = `e2e-profile-${suffix}@example.com`
  const register = await page.request.post(`${API}/auth/register`, {
    data: { name, email, password: 'Password123!' },
  })
  expect(register.ok(), 'register').toBeTruthy()
  const { id } = (await register.json()) as { id: string }

  // The dev side channel only exposes the *latest* verification link, which a concurrent
  // registration (e.g. the other project running the same test) can overwrite. If the latest
  // link isn't this account's, regenerate our own via resend-verification (page.request is
  // signed in as the just-registered user) until we capture it.
  let confirmUrl = ''
  await expect
    .poll(
      async () => {
        const url = await latestVerificationUrl(request)
        if (new URL(url).searchParams.get('userId') === id) {
          confirmUrl = url
          return true
        }
        await page.request.post(`${API}/auth/resend-verification`)
        return false
      },
      { timeout: 15_000 },
    )
    .toBe(true)

  await page.goto(relativePath(confirmUrl))
  await expect(page.getByText('E-post bekräftad')).toBeVisible()
  return { name }
}

test('sets an avatar emoji from the picker and persists it', async ({ page, request }) => {
  await freshVerifiedAccount(page, request)

  await page.goto('/profil')
  await expect(page.getByText('Så här syns du i loggboken.')).toBeVisible()

  // Unset: a single "Välj profilbild" chip, no emoji chips yet.
  await expect(page.getByRole('button', { name: 'Välj profilbild' })).toBeVisible()
  await page.getByRole('button', { name: 'Välj profilbild' }).click()

  // Pick the fox from the curated grid, commit it.
  await expect(page.getByRole('heading', { name: 'Välj profilbild' })).toBeVisible()
  await page.getByRole('button', { name: 'fox' }).click()
  await page.getByRole('button', { name: 'Använd 🦊' }).click()

  await expect(page.getByText('Profilbild uppdaterad')).toBeVisible()
  // Chips flip to the "emoji is set" pair, and the glyph is now on screen.
  await expect(page.getByRole('button', { name: 'Byt emoji' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Ta bort' })).toBeVisible()
  // The glyph now renders in the avatar (decorative, so aria-hidden → assert attached).
  await expect(page.getByText('🦊').first()).toBeAttached()

  // Persisted server-side: a reload still shows the emoji state.
  await page.reload()
  await expect(page.getByRole('button', { name: 'Byt emoji' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Välj profilbild' })).toHaveCount(0)
})

test('resets the avatar back to the letter initial', async ({ page, request }) => {
  await freshVerifiedAccount(page, request)

  // Start with an emoji set, then reset it via the profile "Ta bort" chip.
  await page.goto('/profil')
  await page.getByRole('button', { name: 'Välj profilbild' }).click()
  await page.getByRole('button', { name: 'fox' }).click()
  await page.getByRole('button', { name: 'Använd 🦊' }).click()
  await expect(page.getByRole('button', { name: 'Ta bort' })).toBeVisible()

  await page.getByRole('button', { name: 'Ta bort' }).click()
  await expect(page.getByText('Profilbild uppdaterad')).toBeVisible()

  // Back to the single "Välj profilbild" chip — the letter initial is the fallback again.
  await expect(page.getByRole('button', { name: 'Välj profilbild' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Byt emoji' })).toHaveCount(0)

  await page.reload()
  await expect(page.getByRole('button', { name: 'Välj profilbild' })).toBeVisible()
})

test('toggles the nudge-email preference off and persists it', async ({ page, request }) => {
  await freshVerifiedAccount(page, request)

  await page.goto('/profil')
  // Wait for the profile form to finish loading (me query) before asserting on its controls.
  await expect(page.getByText('Så här syns du i loggboken.')).toBeVisible()
  // Default is on — the "on" hint is shown.
  await expect(page.getByText('Ett dagligt mejl när du har knuffar', { exact: false })).toBeVisible()

  // Turn emails off and save.
  await page.getByRole('button', { name: 'Av' }).click()
  await expect(page.getByText('Du får inga påminnelser via e-post', { exact: false })).toBeVisible()
  await page.getByRole('button', { name: 'Spara' }).click()
  await expect(page.getByText('Profilen sparad')).toBeVisible()

  // Persisted server-side: a reload still shows the off state.
  await page.reload()
  await expect(page.getByText('Du får inga påminnelser via e-post', { exact: false })).toBeVisible()
})

test('picker can reset with "use my letter instead"', async ({ page, request }) => {
  await freshVerifiedAccount(page, request)

  await page.goto('/profil')
  await page.getByRole('button', { name: 'Välj profilbild' }).click()
  await page.getByRole('button', { name: 'fox' }).click()
  await page.getByRole('button', { name: 'Använd 🦊' }).click()
  await expect(page.getByRole('button', { name: 'Byt emoji' })).toBeVisible()

  // Reopen the picker and choose the letter fallback instead.
  await page.getByRole('button', { name: 'Byt emoji' }).click()
  await expect(page.getByRole('heading', { name: 'Välj profilbild' })).toBeVisible()
  await page.getByRole('button', { name: 'Använd min bokstav istället' }).click()

  await expect(page.getByText('Profilbild uppdaterad')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Välj profilbild' })).toBeVisible()
})
