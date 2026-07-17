import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, pinHousehold } from './helpers'

// LEDGER (Loggboken, §2.2): title, filter pills switch the list, day-group headers.
// Assertions about entry presence are scoped to the main feed, because on the
// desktop shell the right rail also renders nudges/upcoming that mention entries.
test.describe('Ledger', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Du')
    const household = await getHouseholdId(page.request, 'Lönnvägen 3')
    await pinHousehold(page, household)
    await page.goto('/ledger')
    await expect(page.getByRole('heading', { name: 'Loggboken' })).toBeVisible()
  })

  test('renders the title, filter pills and day-group headers', async ({ page }) => {
    for (const label of ['Alla', 'Utgifter', 'Repeat']) {
      await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible()
    }
    const feed = page.getByRole('main')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
    // Day-group headers are level-2 headings (Idag / Igår / "{d} {mån}").
    await expect(feed.getByRole('heading', { level: 2 }).first()).toBeVisible()
  })

  test('the "Utgifter" filter shows only expenses', async ({ page }) => {
    const feed = page.getByRole('main')
    await page.getByRole('button', { name: 'Utgifter', exact: true }).click()
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
  })
})
