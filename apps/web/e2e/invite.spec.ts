import { test, expect } from '@playwright/test'
import {
  createHousehold,
  getMembers,
  latestDevInviteToken,
  loginAs,
  openHouseholdSwitcher,
  pinHousehold,
  uniqueSuffix,
} from './helpers'

// INVITES (ADR-0011). Sends an invite from the household switcher's invite
// section, accepts it as a brand-new account, and confirms both accounts share
// the household. Runs in a throwaway household so it never disturbs the seeded
// books other specs assert against.
test('invite a new email, accept it, and see the shared household', async ({ page }) => {
  await loginAs(page, 'Du')

  const suffix = uniqueSuffix()
  const householdName = `E2E Inbjudan ${suffix}`
  const household = await createHousehold(page.request, householdName)
  await pinHousehold(page, household.id)
  await page.goto('/')
  await expect(page.getByText(`öppna poster i ${householdName}`)).toBeVisible()

  const inviteEmail = `e2e-invitee-${suffix}@example.com`

  await openHouseholdSwitcher(page, householdName)
  await page.getByLabel('E-post').fill(inviteEmail)
  await page.getByRole('button', { name: 'Skicka' }).click()
  await expect(page.getByText(`Inbjudan skickad till ${inviteEmail}`)).toBeVisible()
  await expect(page.getByText(`${inviteEmail} — väntar på svar`)).toBeVisible()

  const token = await latestDevInviteToken(page.request)

  // Accept as the brand-new invitee — this replaces Du's session with theirs
  // (the accept endpoint signs the newly created account in).
  await page.goto(`/accept-invite?token=${token}`)
  await expect(page.getByText('har bjudit in dig')).toBeVisible()
  await page.getByLabel('Ditt namn').fill('E2E Inbjuden')
  await page.getByLabel('Lösenord').fill('Password123!')
  await page.getByRole('button', { name: 'Skapa konto & gå med' }).click()

  await expect(page).toHaveURL('http://localhost:5173/')
  await expect(page.getByText(`öppna poster i ${householdName}`)).toBeVisible()

  // Both accounts are now members of the household.
  const members = await getMembers(page.request, household.id)
  expect(members.map((m) => m.name)).toContain('E2E Inbjuden')
  expect(members).toHaveLength(2)
})

test('invite preview 404s for an unknown token', async ({ page }) => {
  const res = await page.request.get('http://localhost:5000/invites/not-a-real-token')
  expect(res.status()).toBe(404)
})
