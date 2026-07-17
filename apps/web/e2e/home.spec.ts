import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, pinHousehold } from './helpers'

// HUSHÅLLET — the merged book page (ADR-0021): net hero, per-person balances
// each with a prominent "Gör upp" action, and the full Loggbok folded in below.
// "Du" belongs to 2 seeded books, so `/` is the overview; the focused single-book
// page lives at `/hushall/$id` (and the Hushållet tab at `/hushallet`). This spec
// exercises the shared merged page directly via the focused route.
test.describe('Hushållet page', () => {
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

  test('lists per-person balances with a prominent "Gör upp" action', async ({ page }) => {
    const row = page.getByTestId('person-row').first()
    await expect(row).toBeVisible()
    // Seed household members other than "Du".
    await expect(page.getByText('Sam', { exact: true }).first()).toBeVisible()
    // The settle action is surfaced on the row itself (ADR-0021), not tap-to-discover.
    await expect(row.getByRole('button', { name: 'Gör upp' })).toBeVisible()
  })

  test('folds the Loggbok into the page (filter pills + entries)', async ({ page }) => {
    for (const label of ['Alla', 'Utgifter', 'Repeat']) {
      await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible()
    }
    await expect(page.getByRole('main').getByText('Begagnad soffa')).toBeVisible()
  })
})
