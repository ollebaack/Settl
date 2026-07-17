import { test, expect, type Page } from '@playwright/test'
import {
  API,
  acceptInvite,
  createHousehold,
  createOneOwesAll,
  inviteAndGetToken,
  pinHousehold,
  registerConfirmedUser,
  uniqueSuffix,
} from './helpers'

// SETTLEMENT HISTORY (docs/specs/settlement-history.md). The read-only "Tidigare
// uppgörelser med {namn}" section on the Settle-up sheet.
//
// Fully isolated: the acting user AND the second member are both freshly-registered
// throwaway accounts (unique emails), never the seeded "Du". This keeps these tests
// from adding households/contacts to a shared seeded member — which would bloat the
// household switcher and destabilise Du-based specs like invite.spec (the "Skicka"
// button gets pushed out of the viewport). Same reasoning as overview.spec.

/** Log in an arbitrary registered account via the real cookie session; returns its id. */
async function loginEmail(page: Page, email: string, password: string): Promise<string> {
  const res = await page.request.post(`${API}/auth/login`, { data: { email, password } })
  expect(res.ok(), `login ${email}`).toBeTruthy()
  return ((await res.json()) as { id: string }).id
}

/** A fresh owner + a fresh second member sharing a brand-new household. */
async function pairHousehold(page: Page, label: string) {
  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const password = 'Password123!'
  const owner = { name: `E2E Ägare ${suffix}`, email: `e2e-hist-owner-${suffix}@example.com`, password }
  const mate = { name: `E2E Medbo ${suffix}`, email: `e2e-hist-mate-${suffix}@example.com`, password }

  const ownerId = await registerConfirmedUser(page, owner) // page.request is now the owner
  const household = await createHousehold(page.request, `E2E ${label} ${suffix}`)
  const token = await inviteAndGetToken(page.request, household.id, mate.email)

  const mateId = await registerConfirmedUser(page, mate) // page.request is now the mate
  await acceptInvite(page.request, token)
  await loginEmail(page, owner.email, owner.password) // back to the acting user

  return { ownerId, householdId: household.id, mateId, mateName: mate.name }
}

test('past settlements show for a pair and expand to their closed entries', async ({ page }) => {
  const { ownerId, householdId, mateId, mateName } = await pairHousehold(page, 'Historik')

  // The mate owes the owner 500 kr, then the owner records a settlement closing the
  // pair (via the API — the same contract the sheet's "Markera allt som reglerat" uses).
  await createOneOwesAll(page.request, householdId, 'E2E skuld', 50000, mateId, ownerId)
  const settled = await page.request.post(`${API}/households/${householdId}/settlements`, {
    data: { personMemberId: mateId },
  })
  expect(settled.ok(), 'record settlement').toBeTruthy()

  await pinHousehold(page, householdId)
  await page.goto(`/hushall/${householdId}`)

  await page.getByTestId('person-row').first().click()
  const sheet = page.getByLabel(`Gör upp med ${mateName}`)
  await expect(page.getByRole('heading', { name: `Gör upp med ${mateName}` })).toBeVisible()

  // History section present with a row, initiated by the acting user, closing one entry.
  await expect(sheet.getByText(`Tidigare uppgörelser med ${mateName}`)).toBeVisible()
  await expect(sheet.getByText('Du gjorde upp · 1 post')).toBeVisible()

  // The closed entry is revealed only after tapping the row.
  await expect(sheet.getByText('E2E skuld')).toBeHidden()
  await page.getByTestId('settlement-row').first().click()
  await expect(sheet.getByText('E2E skuld')).toBeVisible()
})

test('empty state when a pair has never settled', async ({ page }) => {
  const { ownerId, householdId, mateId, mateName } = await pairHousehold(page, 'Historik tom')

  // A live debt but no settlement recorded → history is empty.
  await createOneOwesAll(page.request, householdId, 'E2E öppen', 50000, mateId, ownerId)

  await pinHousehold(page, householdId)
  await page.goto(`/hushall/${householdId}`)

  await page.getByTestId('person-row').first().click()
  const sheet = page.getByLabel(`Gör upp med ${mateName}`)
  await expect(sheet.getByText(`Tidigare uppgörelser med ${mateName}`)).toBeVisible()
  await expect(sheet.getByText('Inga tidigare uppgörelser än')).toBeVisible()
})
