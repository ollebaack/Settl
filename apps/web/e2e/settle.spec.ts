import { test, expect } from '@playwright/test'
import {
  createHousehold,
  createIou,
  getMemberId,
  getMembers,
  pin,
  uniqueSuffix,
} from './helpers'

// SETTLE-UP (Gör upp, §2.8 + flow §4). Runs in a throwaway household created per
// test invocation so settling the whole pair cannot disturb the seeded books
// that the other specs assert against (fully isolated; no restore needed).
test('settle up with a person closes the pair', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const sam = await getMemberId(request, 'Sam')

  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const household = await createHousehold(request, du, `E2E Gör upp ${suffix}`, [du, sam])

  // Members are ordered by join order; find Sam in the new book.
  const members = await getMembers(request, du, household.id)
  const samMember = members.find((m) => m.id === sam)!

  // Sam owes Du 500 kr → on Home this shows as a non-square person row.
  await createIou(request, du, household.id, 'E2E skuld', 50000, sam, du)

  await pin(page, du, household.id)
  await page.goto('/')

  // Open settle-up from the person row.
  await page.getByTestId('person-row').first().click()

  const sheet = page.getByLabel(`Gör upp med ${samMember.name}`)
  await expect(page.getByRole('heading', { name: `Gör upp med ${samMember.name}` })).toBeVisible()
  await expect(sheet.getByText(`${samMember.name} är skyldig dig`)).toBeVisible()
  await expect(sheet.getByText('E2E skuld')).toBeVisible()

  await page.getByRole('button', { name: 'Markera allt som reglerat' }).click()
  await expect(page.getByText(`Uppgjort med ${samMember.name} — rent bord`)).toBeVisible()
})
