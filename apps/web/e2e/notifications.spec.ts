import { test, expect } from '@playwright/test'
import { API, createExpense, getHouseholdId, loginAs, pinHousehold, uniqueSuffix } from './helpers'

// Trust notifications (trust-notifications-v1 spec): when ANOTHER member changes
// something that affects your money, it shows up on the Notiser screen under "Ändringar som
// rör dig" — so you can't be quietly cheated. Each test acts as Sam (the mutator), then logs
// in as Du (a fellow party) to read the notification. Own-actions never notify the actor, so
// reading as Du — not Sam — is what proves the projection.

// Money.FormatKr renders a non-breaking space before "kr" (U+00A0); match it explicitly.
const kr = (major: number) => new RegExp(`${major}\\u00A0kr`)

test('a deleted entry notifies the other party', async ({ page }) => {
  const sam = await loginAs(page, 'Sam')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  const title = `E2E notis borttagen ${uniqueSuffix()}`

  // Sam creates then deletes an equal-split expense (Du carries a share → is a party).
  const { id } = await createExpense(page.request, household, title, 30000, sam)
  const del = await page.request.delete(`${API}/entries/${id}`)
  expect(del.ok(), 'delete as Sam').toBeTruthy()

  // Read as Du.
  await loginAs(page, 'Du')
  await pinHousehold(page, household)
  await page.goto('/activity')

  await expect(page.getByText('Ändringar som rör dig')).toBeVisible()
  await expect(page.getByText(new RegExp(`Sam tog bort.*${title}`))).toBeVisible()
})

test('an edited amount notifies with a before → after change', async ({ page }) => {
  const sam = await loginAs(page, 'Sam')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  const title = `E2E notis ändrad ${uniqueSuffix()}`

  const { id } = await createExpense(page.request, household, title, 30000, sam)
  const put = await page.request.put(`${API}/entries/${id}`, {
    data: {
      type: 'expense',
      title,
      amountMinor: 60000,
      date: null,
      paidByMemberId: sam,
      split: null,
      category: null,
    },
  })
  expect(put.ok(), 'edit amount as Sam').toBeTruthy()

  await loginAs(page, 'Du')
  await pinHousehold(page, household)
  await page.goto('/activity')

  // Scope to THIS notification's card (the shared e2e DB may hold others' edits too).
  const card = page.locator('[data-slot="card"]').filter({ hasText: title })
  await expect(card.getByText(new RegExp(`Sam ändrade.*${title}`))).toBeVisible()
  await expect(card.getByText('Belopp:')).toBeVisible()
  await expect(card.getByText(kr(300))).toBeVisible() // before
  await expect(card.getByText(kr(600))).toBeVisible() // after
})
