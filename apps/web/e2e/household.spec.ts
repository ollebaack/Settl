import { test, expect } from '@playwright/test'
import { getHouseholdId, getMemberId, openHouseholdSwitcher, pin } from './helpers'

// HOUSEHOLD SWITCH (Dina hushåll, §2.5): switch to "Familjen" and back. User↔
// household is many-to-many; "Du" belongs to both seeded books.
test('switch household to Familjen and back to Lönnvägen 3', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const lonnvagen = await getHouseholdId(request, du, 'Lönnvägen 3')
  await pin(page, du, lonnvagen)
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
