import { test, expect } from '@playwright/test'
import {
  API,
  acceptInvite,
  createHousehold,
  createOneOwesAll,
  inviteAndGetToken,
  loginAs,
  pinHousehold,
  registerConfirmedUser,
  uniqueSuffix,
} from './helpers'

// SETTLEMENT HISTORY (docs/specs/settlement-history.md). The read-only "Tidigare
// uppgörelser med {namn}" section on the Settle-up sheet. Each test runs in a
// throwaway household with a freshly-registered second member (unique email → no
// collision on the shared dev-invite slot), so settling the pair can't disturb
// the seeded books other specs assert against.

/** Du + a unique fresh member sharing a new household. Returns ids + the mate's display name. */
async function pairHousehold(page: import('@playwright/test').Page, label: string) {
  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const du = await loginAs(page, 'Du')
  const household = await createHousehold(page.request, `E2E ${label} ${suffix}`)

  const mate = {
    name: `E2E Medbo ${suffix}`,
    email: `e2e-history-${suffix}@example.com`,
    password: 'Password123!',
  }
  const token = await inviteAndGetToken(page.request, household.id, mate.email)
  const mateId = await registerConfirmedUser(page, mate) // page.request is now the mate
  await acceptInvite(page.request, token)
  await loginAs(page, 'Du') // back to the acting user

  return { du, householdId: household.id, mateId, mateName: mate.name }
}

test('past settlements show for a pair and expand to their closed entries', async ({ page }) => {
  const { du, householdId, mateId, mateName } = await pairHousehold(page, 'Historik')

  // The mate owes Du 500 kr, then Du records a settlement closing the pair (via the
  // API — the same contract the sheet's "Markera allt som reglerat" uses).
  await createOneOwesAll(page.request, householdId, 'E2E skuld', 50000, mateId, du)
  const settled = await page.request.post(`${API}/households/${householdId}/settlements`, {
    data: { personMemberId: mateId },
  })
  expect(settled.ok(), 'record settlement').toBeTruthy()

  await pinHousehold(page, householdId)
  await page.goto(`/hushall/${householdId}`)

  await page.getByTestId('person-row').first().click()
  const sheet = page.getByLabel(`Gör upp med ${mateName}`)
  await expect(page.getByRole('heading', { name: `Gör upp med ${mateName}` })).toBeVisible()

  // History section present with a row, initiated by Du, closing one entry.
  await expect(sheet.getByText(`Tidigare uppgörelser med ${mateName}`)).toBeVisible()
  await expect(sheet.getByText('Du gjorde upp · 1 post')).toBeVisible()

  // The closed entry is revealed only after tapping the row.
  await expect(sheet.getByText('E2E skuld')).toBeHidden()
  await page.getByTestId('settlement-row').first().click()
  await expect(sheet.getByText('E2E skuld')).toBeVisible()
})

test('empty state when a pair has never settled', async ({ page }) => {
  const { du, householdId, mateId, mateName } = await pairHousehold(page, 'Historik tom')

  // A live debt but no settlement recorded → history is empty.
  await createOneOwesAll(page.request, householdId, 'E2E öppen', 50000, mateId, du)

  await pinHousehold(page, householdId)
  await page.goto(`/hushall/${householdId}`)

  await page.getByTestId('person-row').first().click()
  const sheet = page.getByLabel(`Gör upp med ${mateName}`)
  await expect(sheet.getByText(`Tidigare uppgörelser med ${mateName}`)).toBeVisible()
  await expect(sheet.getByText('Inga tidigare uppgörelser än')).toBeVisible()
})
