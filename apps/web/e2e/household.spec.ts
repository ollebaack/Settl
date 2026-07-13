import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, openHouseholdSwitcher, pinHousehold } from './helpers'

// HOUSEHOLD SWITCH (Dina hushåll, §2.5): switch to "Familjen" and back. User↔
// household is many-to-many; "Du" belongs to both seeded books.
test('switch household to Familjen and back to Lönnvägen 3', async ({ page }) => {
  await loginAs(page, 'Du')
  const lonnvagen = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, lonnvagen)
  await page.goto('/')
  await expect(page.getByText(/öppna poster i Lönnvägen 3/)).toBeVisible()

  // Switch to Familjen.
  await openHouseholdSwitcher(page, 'Lönnvägen 3')
  await page.getByRole('button', { name: /Familjen/ }).click()
  await expect(page.getByText(/öppna poster i Familjen/)).toBeVisible()

  // Switch back.
  await openHouseholdSwitcher(page, 'Familjen')
  await page.getByRole('button', { name: /Lönnvägen 3/ }).click()
  await expect(page.getByText(/öppna poster i Lönnvägen 3/)).toBeVisible()
})
