import { test, expect } from '@playwright/test'
import { uniqueSuffix } from './helpers'

// LOGIN & SIGNUP (ADR-0011). Uses a brand-new account per run so it never
// collides with seeded members or other specs. The account menu's accessible
// name is a stable aria-label (its visible text is desktop-only, see
// components/account-menu.tsx), so the same selector works on both projects.
test('unauthenticated visitors are redirected to /login', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByText('Välkommen tillbaka')).toBeVisible()
})

test('signs up, logs out, and logs back in', async ({ page }) => {
  const suffix = uniqueSuffix()
  const name = `E2E Person ${suffix}`
  const email = `e2e-${suffix}@example.com`
  const password = 'Password123!'

  await page.goto('/signup')
  await page.getByLabel('Namn').fill(name)
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Skapa konto' }).click()

  // Signed in — lands in the app shell (no household yet, so home shows its
  // loading state, but the shell chrome — including the account menu — renders).
  await expect(page).toHaveURL('http://localhost:5173/')
  await expect(page.getByRole('button', { name: `Konto: ${name}` })).toBeVisible()

  // Log out via the account menu.
  await page.getByRole('button', { name: `Konto: ${name}` }).click()
  await page.getByRole('menuitem', { name: 'Logga ut' }).click()
  await expect(page).toHaveURL(/\/login$/)

  // Log back in with the same credentials.
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Logga in' }).click()
  await expect(page).toHaveURL('http://localhost:5173/')
})

test('shows an inline error for the wrong password', async ({ page }) => {
  const suffix = uniqueSuffix()
  const email = `e2e-${suffix}@example.com`

  await page.goto('/signup')
  await page.getByLabel('Namn').fill(`E2E Fel ${suffix}`)
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill('CorrectPassword123!')
  await page.getByRole('button', { name: 'Skapa konto' }).click()
  await expect(page).toHaveURL('http://localhost:5173/')

  await page.getByRole('button', { name: /Konto:/ }).click()
  await page.getByRole('menuitem', { name: 'Logga ut' }).click()

  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill('WrongPassword!')
  await page.getByRole('button', { name: 'Logga in' }).click()

  await expect(page.getByText('Fel e-post eller lösenord.')).toBeVisible()
  await expect(page).toHaveURL(/\/login$/)
})
