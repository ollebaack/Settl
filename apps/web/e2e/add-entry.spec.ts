import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, openAddSheet, pinHousehold, uniqueSuffix } from './helpers'

// ADD ENTRY (Ny post, §2.6 + flow §4): create an equal expense, and the live
// percent-split validation that blocks save. Data is created with a unique title
// per run so parallel workers / both projects never collide.
test.describe('Add entry', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Du')
    const household = await getHouseholdId(page.request, 'Lönnvägen 3')
    await pinHousehold(page, household)
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

  test('recurring tab drawer does not scroll horizontally', async ({ page }, testInfo) => {
    // The x-overflow lived on the mobile bottom Drawer (a centered Dialog on desktop
    // sizes differently and never showed it). The recurring tab's wide cadence toggle
    // ("Varje månad / Varannan vecka / Varje vecka") was the trigger.
    test.skip(testInfo.project.name !== 'mobile', 'drawer is mobile-only')

    // Pin a narrow-but-common Android width (360px). Pixel 7's 412px is wide enough that the
    // cadence toggle just fits; the overflow only bit on narrower phones — which is what the
    // report was about.
    await page.setViewportSize({ width: 360, height: 800 })

    await openAddSheet(page)
    await page.getByRole('tab', { name: 'Återkommande' }).click()
    await expect(page.getByRole('button', { name: 'Varannan vecka' })).toBeVisible()

    // Two symptoms of the old flex-1 columns: (a) the scroll region gaining an inline
    // scrollbar, and (b) the cadence chips clipping their own label (scrollWidth beyond
    // clientWidth). scrollWidth still reports clipped content, so both survive the drawer's
    // overflow-x-hidden guard. Natural-width wrapping chips avoid both.
    const measure = await page.evaluate(() => {
      const popup = document.querySelector('[data-slot="drawer-popup"]')!
      const scroller = Array.from(popup.querySelectorAll('div')).find(
        (el) => getComputedStyle(el).overflowY === 'auto',
      )
      const clipped = (Array.from(popup.querySelectorAll('[data-slot="toggle-group-item"]')) as HTMLElement[])
        .filter((el) => el.scrollWidth - el.clientWidth > 1)
        .map((el) => el.textContent)
      return {
        scroller: scroller
          ? { scrollWidth: scroller.scrollWidth, clientWidth: scroller.clientWidth }
          : null,
        clipped,
      }
    })
    expect(measure.scroller, 'found the drawer scroll region').not.toBeNull()
    expect(measure.scroller!.scrollWidth).toBeLessThanOrEqual(measure.scroller!.clientWidth)
    expect(measure.clipped, 'no toggle chip clips its label').toEqual([])
  })

  test('blocks save when the whole amount lands on the payer', async ({ page }) => {
    await openAddSheet(page)
    await page.getByRole('textbox', { name: 'Belopp' }).fill('100')

    // Percent split summing to 100 but entirely on the payer (Du) — nobody else owes.
    await page.getByRole('button', { name: '%', exact: true }).click()
    await page.getByRole('textbox', { name: 'Du andel' }).fill('100')
    await page.getByRole('textbox', { name: 'Sam andel' }).fill('0')
    await page.getByRole('textbox', { name: 'Priya andel' }).fill('0')

    await page.getByRole('button', { name: 'Lägg i loggboken' }).click()

    // Save is blocked: a shared expense must include someone other than the payer.
    await expect(page.getByText('Lägg till någon att dela med')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Ny post' })).toBeVisible()
  })
})
