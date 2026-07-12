import { test, expect } from '@playwright/test'
import { createExpense, getHouseholdId, getMemberId, pin, uniqueSuffix } from './helpers'

// ENTRY DETAIL + settle/reopen (§2.7 + flow §4). Uses a freshly-created,
// uniquely-titled entry so it is fully isolated, and round-trips settle→reopen
// so no shared state is left mutated.
test('entry detail settles and reopens (round-trip)', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const household = await getHouseholdId(request, du, 'Lönnvägen 3')
  const title = `E2E detalj ${uniqueSuffix()}`
  await createExpense(request, du, household, title, 24000) // 240 kr, below nudge thresholds

  await pin(page, du, household)
  await page.goto('/ledger')

  // Open the entry from its ledger row.
  await page.getByRole('main').getByText(title).click()

  // Detail sheet: type badge, payer/split meta, share rows. Scope to the sheet
  // (its accessible name is the entry title) — the ledger feed behind it also
  // has rows with "Du betalade".
  const sheet = page.getByLabel(title)
  await expect(sheet.getByText('Utgift', { exact: true })).toBeVisible()
  await expect(sheet.getByText('Du betalade · Delas lika')).toBeVisible()
  await expect(sheet.getByText('· betalade').first()).toBeVisible()

  const settleBtn = page.getByRole('button', { name: 'Markera som reglerad' })
  await expect(settleBtn).toBeVisible()

  // Settle.
  await settleBtn.click()
  await expect(page.getByText('Reglerad — snyggt')).toBeVisible()
  const reopenBtn = page.getByRole('button', { name: /Reglerad ✓ — tryck för att öppna igen/ })
  await expect(reopenBtn).toBeVisible()

  // Reopen — restores original state.
  await reopenBtn.click()
  await expect(page.getByText('Öppnad igen')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Markera som reglerad' })).toBeVisible()
})
