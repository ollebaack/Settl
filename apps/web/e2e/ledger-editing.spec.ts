import { test, expect } from '@playwright/test'
import {
  createExpense,
  createRecurring,
  getHouseholdId,
  loginAs,
  openAddSheet,
  pinHousehold,
  uniqueSuffix,
} from './helpers'

// LEDGER EDITING (ledger-editing spec + addendum): the "Allt på en" split preset,
// entry edit via the ⋯ menu, the deferred delete + Ångra undo toast, and recurring
// delete-if-clean / pause-when-history. Each test creates its own uniquely-titled data
// (or only reads shared seed data) so it stays isolated across parallel workers.

test('split preset "Allt på en" puts the whole amount on one person', async ({ page }) => {
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/')

  const title = `E2E allt-på-en ${uniqueSuffix()}`
  await openAddSheet(page)
  await page.getByRole('textbox', { name: 'Belopp' }).fill('300')
  await page.getByRole('textbox', { name: 'Titel' }).fill(title)

  // Fourth Delning preset → a single-select "who owes it all" picker appears.
  await page.getByRole('button', { name: 'Allt', exact: true }).click()
  await expect(page.getByText('Vem står för hela beloppet?')).toBeVisible()
  // The whole-owner buttons follow the payer buttons, so Sam's second occurrence is here.
  await page.getByRole('button', { name: 'Sam' }).nth(1).click()
  await expect(page.getByText('Sam står för hela beloppet')).toBeVisible()

  await page.getByRole('button', { name: 'Lägg i loggboken' }).click()
  await expect(page.getByText('Tillagd i loggboken')).toBeVisible()

  // It lands as ONE ordinary expense (not also an IOU): Du paid, Sam owes the full amount.
  await page.goto('/ledger')
  await page.getByRole('main').getByText(title).click()
  const sheet = page.getByLabel(title)
  await expect(sheet.getByText('Utgift', { exact: true })).toBeVisible()
  await expect(sheet.getByText('Du betalade · Egna belopp')).toBeVisible()
  await expect(sheet.getByText('· skyldig').first()).toBeVisible()
  await expect(sheet.getByText(/300/).first()).toBeVisible()
})

test('edit an entry from the ⋯ menu (prefilled, saves changes)', async ({ page }) => {
  const du = await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  const title = `E2E redigera ${uniqueSuffix()}`
  await createExpense(page.request, household, title, 15000, du)

  await pinHousehold(page, household)
  await page.goto('/ledger')
  await page.getByRole('main').getByText(title).click()

  await page.getByLabel(title).getByRole('button', { name: 'Fler åtgärder' }).click()
  await page.getByRole('menuitem', { name: 'Redigera' }).click()

  // The add-entry form opens in edit mode, prefilled from the entry.
  const editSheet = page.getByLabel('Redigera post')
  await expect(page.getByRole('heading', { name: 'Redigera post' })).toBeVisible()
  const titleBox = editSheet.getByRole('textbox', { name: 'Titel' })
  await expect(titleBox).toHaveValue(title)

  const newTitle = `${title} v2`
  await titleBox.fill(newTitle)
  await editSheet.getByRole('button', { name: 'Spara ändringar' }).click()

  await expect(page.getByText('Ändrad', { exact: true })).toBeVisible()
  await page.goto('/ledger')
  await expect(page.getByRole('main').getByText(newTitle)).toBeVisible()
})

test('delete shows an Ångra toast that cancels the delete', async ({ page }) => {
  const du = await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  const title = `E2E undo ${uniqueSuffix()}`
  await createExpense(page.request, household, title, 12000, du)

  await pinHousehold(page, household)
  await page.goto('/ledger')
  await page.getByRole('main').getByText(title).click()

  await page.getByLabel(title).getByRole('button', { name: 'Fler åtgärder' }).click()
  await page.getByRole('menuitem', { name: 'Ta bort' }).click()

  // Confirm dialog, then the deferred delete + undo toast.
  await expect(page.getByRole('heading', { name: 'Ta bort posten?' })).toBeVisible()
  await page.getByRole('button', { name: 'Ta bort' }).click()
  await expect(page.getByText('Posten togs bort')).toBeVisible()

  // Ångra cancels the pending delete — nothing hit the server, so the entry survives a reload.
  await page.getByRole('button', { name: 'Ångra', exact: true }).click()
  await page.reload()
  await expect(page.getByRole('main').getByText(title)).toBeVisible()
})

test('delete commits after the undo window elapses', async ({ page }) => {
  const du = await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  const title = `E2E ta-bort ${uniqueSuffix()}`
  await createExpense(page.request, household, title, 11000, du)

  await pinHousehold(page, household)
  await page.goto('/ledger')
  await page.getByRole('main').getByText(title).click()

  await page.getByLabel(title).getByRole('button', { name: 'Fler åtgärder' }).click()
  await page.getByRole('menuitem', { name: 'Ta bort' }).click()
  await expect(page.getByRole('heading', { name: 'Ta bort posten?' })).toBeVisible()
  await page.getByRole('button', { name: 'Ta bort' }).click()
  await expect(page.getByText('Posten togs bort')).toBeVisible()

  // Wait past the ~5 s window so the real DELETE fires, then confirm it's gone for good.
  await page.waitForTimeout(6000)
  await page.reload()
  await expect(page.getByRole('main').getByText(title)).toBeHidden()
})

test('recurring: clean template can be hard-deleted', async ({ page }) => {
  const du = await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  const title = `E2E mall ${uniqueSuffix()}`
  const far = new Date(Date.now() + 40 * 86_400_000).toISOString().slice(0, 10)
  await createRecurring(page.request, household, title, 25000, far, du)

  await pinHousehold(page, household)
  await page.goto('/recurring')
  const card = page.locator('[data-slot="card"]', { hasText: title })
  await card.getByRole('button', { name: 'Detaljer' }).click()

  await page.getByLabel(title).getByRole('button', { name: 'Fler åtgärder' }).click()
  await page.getByRole('menuitem', { name: 'Ta bort' }).click()
  await expect(page.getByText(`Ta bort ”${title}”?`)).toBeVisible()
  await page.getByRole('button', { name: 'Ta bort' }).click()

  // The card disappearing is the durable outcome; the "borttagen" toast auto-dismisses.
  await expect(page.locator('[data-slot="card"]', { hasText: title })).toBeHidden()
})

test('recurring: template with history offers pause, not delete', async ({ page }) => {
  // "Hyra" has a posted period in the seed → delete is disabled, only pause is offered.
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/recurring')

  const card = page.locator('[data-slot="card"]', { hasText: 'Hyra' })
  await card.getByRole('button', { name: 'Detaljer' }).click()

  const sheet = page.getByLabel('Hyra')
  await sheet.getByRole('button', { name: 'Fler åtgärder' }).click()
  await expect(
    page.getByText('Har bokförda perioder — pausa i stället, så finns historiken kvar.'),
  ).toBeVisible()
  await expect(page.getByRole('menuitem', { name: 'Ta bort' })).toBeDisabled()
})
