import { test, expect } from '@playwright/test'
import { getHouseholdId, getMemberId, pin } from './helpers'

// NUDGES (Knuffar, §2.4): the activity view shows nudge cards. Two seeded
// triggers are stable across the suite: the rent recurring-due nudge and the
// big-expense ("Begagnad soffa") nudge. (Uses .first() because the desktop shell
// also mirrors nudges in the right rail.)
test('activity view shows the rent and big-expense nudges', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const household = await getHouseholdId(request, du, 'Lönnvägen 3')
  await pin(page, du, household)
  await page.goto('/activity')

  await expect(page.getByRole('heading', { name: 'Knuffar' })).toBeVisible()

  // Recurring-due nudge for the rent template (direct tone: "{titel} dras {när}").
  await expect(page.getByText(/Hyra dras/).first()).toBeVisible()

  // Big-expense nudge for the seeded ≥1500 kr sofa expense.
  await expect(page.getByText('Stor utgift: Begagnad soffa').first()).toBeVisible()
})
