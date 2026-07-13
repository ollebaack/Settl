import { test, expect } from '@playwright/test'
import {
  acceptInvite,
  createHousehold,
  createIou,
  getMembers,
  inviteAndGetToken,
  loginAs,
  pinHousehold,
  uniqueSuffix,
} from './helpers'

// SETTLE-UP (Gör upp, §2.8 + flow §4). Runs in a throwaway household created per
// test invocation so settling the whole pair cannot disturb the seeded books
// that the other specs assert against (fully isolated; no restore needed).
// Sam joins via a real invite (ADR-0011 — households are invite-only), so the
// login switches Du → Sam → Du before the actual UI test runs as Du.
test('settle up with a person closes the pair', async ({ page }) => {
  const du = await loginAs(page, 'Du')

  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const household = await createHousehold(page.request, `E2E Gör upp ${suffix}`)
  const token = await inviteAndGetToken(page.request, household.id, 'sam@settl.dev')

  const sam = await loginAs(page, 'Sam')
  await acceptInvite(page.request, token)
  await loginAs(page, 'Du')

  // Members are ordered by join order; find Sam in the new book.
  const members = await getMembers(page.request, household.id)
  const samMember = members.find((m) => m.id === sam)!

  // Sam owes Du 500 kr → on Home this shows as a non-square person row.
  await createIou(page.request, household.id, 'E2E skuld', 50000, sam, du)

  await pinHousehold(page, household.id)
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
