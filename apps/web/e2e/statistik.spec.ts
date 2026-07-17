import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, pinHousehold } from './helpers'

// STATISTIK (docs/specs/household-statistics.md): a per-person "who paid how much,
// when" bar chart for the active household. The canonical seed's Lönnvägen 3 has
// recent entries paid by all three members, so the chart renders a series each.
// Legend assertions are scoped to <main> because the desktop right-rail nudges can
// also mention member names.
test('statistik shows a per-person contribution chart for the household', async ({ page }) => {
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/statistik')

  await expect(page.getByRole('heading', { name: 'Statistik' })).toBeVisible()

  // The chart legend names each contributing member (a series per person).
  const main = page.getByRole('main')
  await expect(main.getByText('Sam', { exact: true })).toBeVisible()
  await expect(main.getByText('Priya', { exact: true })).toBeVisible()

  // Not the empty state.
  await expect(page.getByText('Inget att visa än', { exact: false })).toHaveCount(0)
})

test('the Statistik tab replaced Aktivitet in the nav', async ({ page }) => {
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/')

  await page.getByRole('link', { name: 'Statistik' }).filter({ visible: true }).first().click()
  await expect(page).toHaveURL(/\/statistik$/)
  await expect(page.getByRole('heading', { name: 'Statistik' })).toBeVisible()
})

// Knuffar moved off the tab bar into a bell button in the mobile header. Desktop keeps
// its right-rail Knuffar panel, so the header bell only exists on the phone viewport.
test('the mobile header bell opens Knuffar', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'mobile', 'bell button is mobile-only; desktop uses the right rail')

  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/')

  await page.getByRole('link', { name: 'Knuffar' }).click()
  await expect(page).toHaveURL(/\/activity$/)
  await expect(page.getByRole('heading', { name: 'Knuffar' })).toBeVisible()
})
