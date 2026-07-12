import { test, expect } from '@playwright/test'
import { getHouseholdId, getMemberId, openAddSheet, pin, uniqueSuffix } from './helpers'

// ADD ENTRY (Ny post, §2.6 + flow §4): create an equal expense, and the live
// percent-split validation that blocks save. Data is created with a unique title
// per run so parallel workers / both projects never collide.
test.describe('Add entry', () => {
  let du: string
  let household: string

  test.beforeEach(async ({ page, request }) => {
    du = await getMemberId(request, 'Du')
    household = await getHouseholdId(request, du, 'Lönnvägen 3')
    await pin(page, du, household)
    await page.goto('/')
  })

  test('creates an EQUAL expense and it appears in the ledger', async ({ page }) => {
    const title = `E2E utgift ${uniqueSuffix()}`

    await openAddSheet(page)
    await page.getByRole('textbox', { name: 'Belopp' }).fill('120')
    await page.getByRole('textbox', { name: 'Titel' }).fill(title)
    await page.getByRole('button', { name: 'Lägg i loggboken' }).click()

    // Success toast, then the sheet closes.
    await expect(page.getByText('Tillagd i loggboken')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Ny post' })).toBeHidden()

    // It shows up in the ledger feed.
    await page.goto('/ledger')
    await expect(page.getByRole('main').getByText(title)).toBeVisible()
  })

  test('blocks save when percentages do not total 100', async ({ page }) => {
    await openAddSheet(page)
    await page.getByRole('textbox', { name: 'Belopp' }).fill('100')

    // Switch the split mode to percent and enter shares summing to 30 (≠ 100).
    await page.getByRole('button', { name: '%', exact: true }).click()
    await page.getByRole('textbox', { name: 'Du andel' }).fill('10')
    await page.getByRole('textbox', { name: 'Sam andel' }).fill('10')
    await page.getByRole('textbox', { name: 'Priya andel' }).fill('10')

    // Live, color-coded hint reflects the running total.
    await expect(page.getByText(/av 100 % fördelat/)).toBeVisible()

    await page.getByRole('button', { name: 'Lägg i loggboken' }).click()

    // Save is blocked: validation toast fires and the sheet stays open.
    await expect(page.getByText('Procenten måste bli 100')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Ny post' })).toBeVisible()
  })
})
