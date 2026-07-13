import { test, expect } from '@playwright/test'
import { getHouseholdId, getMemberId, getMembers, openHouseholdSwitcher, pin, uniqueSuffix } from './helpers'

// NEW HOUSEHOLD (Nytt hushåll, §2.5b): create a household by name with a typed
// member added inline — no invite step. The acting user is always included and
// the app switches straight into the fresh, empty book.
test('creates a household with a new member and switches into it', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const lonnvagen = await getHouseholdId(request, du, 'Lönnvägen 3')
  await pin(page, du, lonnvagen)
  await page.goto('/')

  const name = `E2E Sommarstugan ${uniqueSuffix()}`

  await openHouseholdSwitcher(page, 'Lönnvägen 3')
  await page.getByRole('button', { name: '+ Nytt hushåll' }).click()
  await expect(page.getByRole('heading', { name: 'Nytt hushåll' })).toBeVisible()

  await page.getByLabel('Namn').fill(name)
  await page.getByRole('button', { name: '+ Lägg till medlem' }).click()
  await page.getByLabel('Medlemsnamn').fill('Alex')
  await page.getByRole('button', { name: 'Skapa hushåll' }).click()

  // Success toast, sheet closes, and the app switched to the new (now active) household.
  await expect(page.getByText(`${name} skapat — tomt blad`)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Nytt hushåll' })).toBeHidden()
  await expect(page.getByText(`öppna poster i ${name}`)).toBeVisible()

  // The household was created with Du + Alex as members, in that order.
  const created = await getHouseholdId(request, du, name)
  const members = await getMembers(request, du, created)
  expect(members.map((m) => m.name)).toEqual(['Du', 'Alex'])
})

test('blocks save when the name is empty', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const lonnvagen = await getHouseholdId(request, du, 'Lönnvägen 3')
  await pin(page, du, lonnvagen)
  await page.goto('/')

  await openHouseholdSwitcher(page, 'Lönnvägen 3')
  await page.getByRole('button', { name: '+ Nytt hushåll' }).click()
  await page.getByRole('button', { name: 'Skapa hushåll' }).click()

  await expect(page.getByText('Ge hushållet ett namn först')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Nytt hushåll' })).toBeVisible()
})
