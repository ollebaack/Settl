import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, pinHousehold } from './helpers'

// NUDGES (Notiser, §2.4): the activity view shows nudge cards. Two seeded
// triggers are stable across the suite: the rent recurring-due nudge and the
// big-expense ("Begagnad soffa") nudge. (Uses .first() because the desktop shell
// also mirrors nudges in the right rail.)
test('activity view shows the rent and big-expense nudges', async ({ page }) => {
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/activity')

  await expect(page.getByRole('heading', { name: 'Notiser' })).toBeVisible()

  // Recurring-due nudge for the rent template (direct tone: "{titel} dras {när}").
  await expect(page.getByText(/Hyra dras/).first()).toBeVisible()

  // Big-expense nudge for the seeded ≥1500 kr sofa expense.
  await expect(page.getByText('Stor utgift: Begagnad soffa').first()).toBeVisible()
})
