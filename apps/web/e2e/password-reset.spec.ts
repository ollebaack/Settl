import { test, expect } from '@playwright/test'
import { latestDevPasswordResetUrl, relativePath, uniqueSuffix } from './helpers'

// FORGOT / RESET PASSWORD (docs/design/auth-onboarding-addendum.md §2.1's "Glömt?" flow).
// Uses a brand-new throwaway account, never a seeded member — resetting a seeded member's
// password would break every other spec's loginAs() against the shared e2e database.
test('forgot password, reset it, and log in with the new password', async ({ page, request }) => {
  const suffix = uniqueSuffix()
  const email = `e2e-reset-${suffix}@example.com`
  const password = 'Password123!'
  const newPassword = 'NewPassword456!'

  await page.goto('/signup')
  await page.getByLabel('Namn').fill(`E2E Reset ${suffix}`)
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Skapa konto' }).click()
  await expect(page).toHaveURL(/\/verify-email$/)

  // Password reset doesn't require a confirmed email — log out without verifying.
  await page.getByRole('button', { name: 'Logga ut' }).click()
  await expect(page).toHaveURL(/\/login$/)

  await page.getByLabel('E-post').fill(email)
  await page.getByRole('button', { name: 'Glömt?' }).click()
  await expect(page.getByText('Kolla din inkorg')).toBeVisible()
  await expect(page.getByText(email)).toBeVisible()

  const resetUrl = await latestDevPasswordResetUrl(request, email)
  await page.goto(relativePath(resetUrl))
  await page.getByLabel('Nytt lösenord').fill(newPassword)
  await page.getByRole('button', { name: 'Spara lösenord' }).click()

  // Reset signs the user in — still unconfirmed, so it lands on /verify-email.
  await expect(page).toHaveURL(/\/verify-email$/)
  await page.getByRole('button', { name: 'Logga ut' }).click()

  // The old password no longer works; the new one does.
  await page.getByLabel('E-post').fill(email)
  await page.getByLabel('Lösenord').fill(password)
  await page.getByRole('button', { name: 'Logga in' }).click()
  await expect(page.getByText('Fel e-post eller lösenord.')).toBeVisible()

  await page.getByLabel('Lösenord').fill(newPassword)
  await page.getByRole('button', { name: 'Logga in' }).click()
  await expect(page).toHaveURL(/\/verify-email$/)
})
