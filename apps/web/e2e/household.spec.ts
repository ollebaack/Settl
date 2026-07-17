import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, openHouseholdSwitcher, pinHousehold } from './helpers'

// MULTI-HOUSEHOLD OVERVIEW drill-in + switch (ADR-0019, §2.1). "Du" belongs to
// both seeded books, so `/` is the overview. Tapping a book enters its focused
// dashboard; the retained in-context switcher still swaps books; the "Hem" tab
// returns to the overview.
test('drill into a book from the overview, switch, and return', async ({ page }) => {
  await loginAs(page, 'Du')
  const lonnvagen = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, lonnvagen)
  await page.goto('/')

  // Two+ active books → the overview, not a single dashboard.
  await expect(page.getByRole('heading', { name: 'Dina hushåll', level: 1 })).toBeVisible()

  // Enter Familjen from its overview card.
  await page.getByTestId('household-card').filter({ hasText: 'Familjen' }).click()
  await expect(page.getByText(/öppna poster i Familjen/)).toBeVisible()

  // The retained switcher still swaps books in-context.
  await openHouseholdSwitcher(page, 'Familjen')
  await page.getByRole('button', { name: /Lönnvägen 3/ }).click()
  await expect(page.getByText(/öppna poster i Lönnvägen 3/)).toBeVisible()

  // Back to the overview from the focused book — the "Hem" tab.
  await page.getByRole('link', { name: 'Hem' }).click()
  await expect(page.getByRole('heading', { name: 'Dina hushåll', level: 1 })).toBeVisible()
})
