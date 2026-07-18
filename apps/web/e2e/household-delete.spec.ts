import { test, expect } from '@playwright/test'
import {
  API,
  createExpense,
  createHousehold,
  pinHousehold,
  registerConfirmedUser,
  uniqueSuffix,
} from './helpers'

// DELETE EMPTY HOUSEHOLD (household-ownership spec). Empty households — created by mistake — can be
// hard-deleted by the owner; a household with any ledger activity can only be archived.
// Each test uses its own freshly-registered owner so the deletion is isolated from the
// shared seed data.

test('owner permanently deletes an empty household', async ({ page }) => {
  const suffix = uniqueSuffix()
  await registerConfirmedUser(page, {
    name: `Radera-${suffix}`,
    email: `radera-${suffix}@settl.dev`,
    password: 'Settl-Dev-123!',
  })
  const name = `Feluppgjort ${suffix}`
  const { id } = await createHousehold(page.request, name)
  await pinHousehold(page, id)

  await page.goto('/household')

  // Empty → the delete affordance is enabled.
  const deleteButton = page.getByRole('button', { name: 'Ta bort hushåll' })
  await expect(deleteButton).toBeEnabled()
  await deleteButton.click()

  // Confirmation sheet, then confirm the terminal action.
  await expect(page.getByRole('heading', { name: `Ta bort ${name}?` })).toBeVisible()
  await page.getByRole('button', { name: 'Ta bort permanent' }).click()

  // The confirmation sheet closes and we land back home — the UI outcomes that used to
  // hang behind an awaited refetch of the just-deleted household. They ride the mutation's
  // success callback, which no longer waits on a 404-ing refetch, so a lingering sheet now
  // means a real regression. The bug was a PERMANENT hang, so a generous timeout keeps this
  // a reliable guard while tolerating a merely-slow close under parallel CI load (which is
  // why #52 had dropped these). (Deleting your only household leaves you with none, so Home
  // opens the create sheet — hence the optional `?sheet=…`; the point is we left /household
  // for the root.) The transient "togs bort" toast is deliberately NOT asserted: sonner
  // auto-dismisses it within seconds, racing any assertion window.
  await expect(page.getByRole('heading', { name: `Ta bort ${name}?` })).toBeHidden({ timeout: 15_000 })
  await expect(page).toHaveURL(/\/(\?.*)?$/, { timeout: 15_000 })

  // …and the household is gone for good — poll this durable server post-condition.
  await expect
    .poll(async () => {
      const res = await page.request.get(`${API}/households`)
      const list = (await res.json()) as Array<{ id: string }>
      return list.some((h) => h.id === id)
    })
    .toBe(false)
})

test('a household with activity cannot be deleted, only archived', async ({ page }) => {
  const suffix = uniqueSuffix()
  const ownerId = await registerConfirmedUser(page, {
    name: `Behall-${suffix}`,
    email: `behall-${suffix}@settl.dev`,
    password: 'Settl-Dev-123!',
  })
  const name = `Aktiv ${suffix}`
  const { id } = await createHousehold(page.request, name)
  // One expense makes it non-empty.
  await createExpense(page.request, id, 'Mjölk', 2_000, ownerId)
  await pinHousehold(page, id)

  await page.goto('/household')

  // Non-empty → the delete affordance is present but disabled; archive remains available.
  await expect(page.getByRole('button', { name: 'Ta bort hushåll' })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Arkivera hushåll' })).toBeEnabled()
  await expect(page.getByText(/kan bara arkiveras, inte tas bort/)).toBeVisible()
})
