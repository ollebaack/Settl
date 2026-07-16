import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, pinHousehold } from './helpers'

// HOME dashboard (Hem, §2.1): net hero, per-person rows, "Senaste", "Visa alla"
// → ledger. "Du" belongs to 2 seeded books, so `/` is the multi-household
// overview (ADR-0019); the focused single-book dashboard lives at `/hushall/$id`.
// This spec exercises that shared dashboard directly via the focused route.
test.describe('Home dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Du')
    const household = await getHouseholdId(page.request, 'Lönnvägen 3')
    await pinHousehold(page, household)
    await page.goto(`/hushall/${household}`)
  })

  test('shows the net hero with a Swedish net label', async ({ page }) => {
    await expect(
      page.getByText(/Du är skyldig totalt|Du ska få totalt|Allt är kvitt/),
    ).toBeVisible()
    // Net sub-line names the active household.
    await expect(page.getByText(/öppna poster i Lönnvägen 3/)).toBeVisible()
  })

  test('lists at least one per-person balance row', async ({ page }) => {
    await expect(page.getByTestId('person-row').first()).toBeVisible()
    // Seed household members other than "Du".
    await expect(page.getByText('Sam', { exact: true }).first()).toBeVisible()
  })

  test('has a "Senaste" section and "Visa alla" navigates to the ledger', async ({ page }) => {
    await expect(page.getByText('Senaste')).toBeVisible()
    await page.getByRole('link', { name: 'Visa alla' }).click()
    await expect(page).toHaveURL(/\/ledger$/)
    await expect(page.getByRole('heading', { name: 'Loggboken' })).toBeVisible()
  })
})
