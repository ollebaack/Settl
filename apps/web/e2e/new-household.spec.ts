import { test, expect } from '@playwright/test'
import { getHouseholdId, getMembers, loginAs, openHouseholdSwitcher, pinHousehold, uniqueSuffix } from './helpers'

// NEW HOUSEHOLD (Nytt hushåll, §2.5b): create a household by name only — every
// other member joins via invite (ADR-0011), not typed in here. The acting user
// is its sole initial member and the app switches straight into the fresh,
// empty book.
test('creates a household and switches into it', async ({ page }) => {
  const du = await loginAs(page, 'Du')
  const lonnvagen = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, lonnvagen)
  await page.goto('/')

  const name = `E2E Sommarstugan ${uniqueSuffix()}`

  await openHouseholdSwitcher(page, 'Lönnvägen 3')
  await page.getByRole('button', { name: '+ Nytt hushåll' }).click()
  await expect(page.getByRole('heading', { name: 'Nytt hushåll' })).toBeVisible()

  await page.getByLabel('Namn').fill(name)
  await page.getByRole('button', { name: 'Skapa hushåll' }).click()

  // Success toast, sheet closes, and the app switched to the new (now active) household.
  await expect(page.getByText(`${name} skapat`)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Nytt hushåll' })).toBeHidden()
  await expect(page.getByText(`öppna poster i ${name}`)).toBeVisible()

  // The household was created with Du as its sole member.
  const created = await getHouseholdId(page.request, name)
  const members = await getMembers(page.request, created)
  expect(members.map((m) => m.id)).toEqual([du])
})

test('blocks save when the name is empty', async ({ page }) => {
  await loginAs(page, 'Du')
  const lonnvagen = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, lonnvagen)
  await page.goto('/')

  await openHouseholdSwitcher(page, 'Lönnvägen 3')
  await page.getByRole('button', { name: '+ Nytt hushåll' }).click()
  await page.getByRole('button', { name: 'Skapa hushåll' }).click()

  await expect(page.getByText('Ge hushållet ett namn först')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Nytt hushåll' })).toBeVisible()
})
