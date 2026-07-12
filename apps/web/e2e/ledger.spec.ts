import { test, expect } from '@playwright/test'
import { getHouseholdId, getMemberId, pin } from './helpers'

// LEDGER (Loggboken, §2.2): title, filter pills switch the list, day-group headers.
// Assertions about entry presence are scoped to the main feed, because on the
// desktop shell the right rail also renders nudges/upcoming that mention entries.
test.describe('Ledger', () => {
  test.beforeEach(async ({ page, request }) => {
    const du = await getMemberId(request, 'Du')
    const household = await getHouseholdId(request, du, 'Lönnvägen 3')
    await pin(page, du, household)
    await page.goto('/ledger')
    await expect(page.getByRole('heading', { name: 'Loggboken' })).toBeVisible()
  })

  test('renders the title, filter pills and day-group headers', async ({ page }) => {
    for (const label of ['Alla', 'Utgifter', 'Lån', 'Repeat']) {
      await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible()
    }
    const feed = page.getByRole('main')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
    // Day-group headers are level-2 headings (Idag / Igår / "{d} {mån}").
    await expect(feed.getByRole('heading', { level: 2 }).first()).toBeVisible()
  })

  test('the "Lån" filter shows IOU entries and hides expenses', async ({ page }) => {
    const feed = page.getByRole('main')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()

    await page.getByRole('button', { name: 'Lån', exact: true }).click()

    await expect(feed.getByText('Konsertbiljett')).toBeVisible()
    await expect(feed.getByText('Begagnad soffa')).toHaveCount(0)
  })

  test('the "Utgifter" filter hides IOU entries', async ({ page }) => {
    const feed = page.getByRole('main')
    await page.getByRole('button', { name: 'Utgifter', exact: true }).click()
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
    await expect(feed.getByText('Konsertbiljett')).toHaveCount(0)
  })
})
