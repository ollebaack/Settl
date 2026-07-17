import { test, expect } from '@playwright/test'
import {
  createHousehold,
  getHouseholdId,
  loginAs,
  pinHousehold,
  registerConfirmedUser,
  uniqueSuffix,
} from './helpers'

// MULTI-HOUSEHOLD OVERVIEW as always-home (ADR-0020). `/` is always the overview,
// regardless of household count: one book → a thinner single-book overview; 2+ →
// the roll-up. "Du" belongs to two seeded books.

test('single active household → the single-book overview (not a collapse)', async ({ page }) => {
  // A fresh account with exactly one household — fully isolated so other specs'
  // shared-DB mutations (e.g. inviting a seeded member elsewhere) can't flip it
  // to multi-household.
  const suffix = uniqueSuffix()
  await registerConfirmedUser(page, {
    name: `E2E Ensam ${suffix}`,
    email: `e2e-ensam-${suffix}@example.com`,
    password: 'Password123!',
  })
  const name = `E2E Enda hushållet ${suffix}`
  await createHousehold(page.request, name)

  await page.goto('/')

  // ADR-0020: still the overview, singular — title "Ditt hushåll", the book as a
  // card with an "Öppna" affordance, and a hero scoped to that book.
  await expect(page.getByRole('heading', { name: 'Ditt hushåll', level: 1 })).toBeVisible()
  const card = page.getByTestId('household-card').filter({ hasText: name })
  await expect(card).toBeVisible()
  await expect(card.getByText('Öppna')).toBeVisible()
  await expect(page.getByText(`i ${name}`)).toBeVisible()
})

test.describe('overview (2+ households, single currency)', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Du')
    const lonnvagen = await getHouseholdId(page.request, 'Lönnvägen 3')
    await pinHousehold(page, lonnvagen)
    await page.goto('/')
    await expect(page.getByRole('heading', { name: 'Dina hushåll', level: 1 })).toBeVisible()
  })

  test('lists every active book as a card', async ({ page }) => {
    await expect(page.getByText('Översikt')).toBeVisible()
    await expect(
      page.getByTestId('household-card').filter({ hasText: 'Lönnvägen 3' }),
    ).toBeVisible()
    await expect(
      page.getByTestId('household-card').filter({ hasText: 'Familjen' }),
    ).toBeVisible()
    await expect(page.getByRole('button', { name: '+ Nytt hushåll' })).toBeVisible()
  })

  test('shows a single summed hero, not the cross-currency roll-up', async ({ page }) => {
    // Both seeded books are SEK, so the hero sums to one headline.
    await expect(
      page.getByText(/Du ska få totalt|Du är skyldig totalt|Allt är kvitt/),
    ).toBeVisible()
    await expect(page.getByText(/över \d+ aktiva hushåll/)).toBeVisible()
    // The mixed-currency roll-up must NOT appear when currencies match.
    await expect(page.getByText('Din ställning')).toHaveCount(0)
    await expect(page.getByText(/Olika valutor/)).toHaveCount(0)
  })

  test('carries the add-a-friend affordance and opens its sheet', async ({ page }) => {
    await expect(page.getByText('Bjud in via nummer eller mejl')).toBeVisible()
    await page.getByTestId('add-friend-card').click()
    await expect(page.getByRole('heading', { name: 'Lägg till en vän' })).toBeVisible()
    await expect(page.getByLabel('Telefonnummer')).toBeVisible()
    await expect(page.getByLabel('E-post')).toBeVisible()
  })
})

test('mixed currencies → descriptive roll-up, never a sum', async ({ page }) => {
  const suffix = uniqueSuffix()
  await registerConfirmedUser(page, {
    name: `E2E Valuta ${suffix}`,
    email: `e2e-valuta-${suffix}@example.com`,
    password: 'Password123!',
  })
  // Two books spanning two currencies → the client must switch to the
  // descriptive hero and never sum across currencies (ADR-0019 §2.2).
  await createHousehold(page.request, `E2E Hemma ${suffix}`, 'SEK')
  await createHousehold(page.request, `E2E Resa ${suffix}`, 'EUR')

  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Dina hushåll', level: 1 })).toBeVisible()
  await expect(page.getByText('Din ställning')).toBeVisible()
  await expect(
    page.getByText('Olika valutor — summeras inte. Öppna ett hushåll för dess saldo.'),
  ).toBeVisible()
  // No summed headline exists in the mixed-currency hero.
  await expect(page.getByText(/få totalt|skyldig totalt/)).toHaveCount(0)
  // Each card carries its own currency.
  await expect(page.getByTestId('household-card').filter({ hasText: 'EUR' })).toBeVisible()
})
