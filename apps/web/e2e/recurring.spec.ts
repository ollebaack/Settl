import { test, expect } from '@playwright/test'
import {
  createRecurring,
  getHouseholdId,
  loginAs,
  openAddSheet,
  pinHousehold,
  uniqueSuffix,
} from './helpers'

// RECURRING (På repeat, §2.3 + flow §4): the screen lists templates with cycle
// progress; pause then resume a template (round-trip restores state). Uses a
// freshly-created, uniquely-titled template scheduled far out (so it adds no
// due-nudge) to stay isolated from the other specs.
test('pause and resume a recurring template (round-trip)', async ({ page }) => {
  const du = await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')

  const title = `E2E repeat ${uniqueSuffix()}`
  const far = new Date(Date.now() + 40 * 86_400_000).toISOString().slice(0, 10)
  await createRecurring(page.request, household, title, 30000, far, du)

  await pinHousehold(page, household)
  await page.goto('/recurring')
  await expect(page.getByRole('heading', { name: 'På repeat' })).toBeVisible()
  // Screen shows cycle progress for templates.
  await expect(page.getByRole('progressbar').first()).toBeVisible()

  // Scope to this template's card.
  const card = page.locator('[data-slot="card"]', { hasText: title })
  await expect(card).toBeVisible()

  // Pause.
  await card.getByRole('button', { name: 'Pausa' }).click()
  await expect(page.getByText(`${title} pausad — inga fler autobokföringar`)).toBeVisible()
  await expect(card.getByText('Pausad')).toBeVisible()
  await expect(card.getByRole('button', { name: 'Återuppta' })).toBeVisible()

  // Resume — restores the active state.
  await card.getByRole('button', { name: 'Återuppta' }).click()
  await expect(page.getByText(`${title} återupptagen`)).toBeVisible()
  await expect(card.getByRole('button', { name: 'Pausa' })).toBeVisible()
})

// END DATE (recurring-end-date spec): a template can be told to stop — "Efter antal" resolves
// to a stored end date server-side; the detail sheet then shows when it slutar.
test('create a recurring template that ends after N times', async ({ page }) => {
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/')

  // Title deliberately free of the word "slutar" so the end-date assertion below is unambiguous.
  const title = `E2E ends ${uniqueSuffix()}`

  await openAddSheet(page)
  await page.getByRole('tab', { name: 'Återkommande' }).click()
  await page.getByRole('textbox', { name: 'Belopp' }).fill('300')
  await page.getByRole('textbox', { name: 'Titel' }).fill(title)

  // Pick "Efter antal" and set 3 occurrences → the API resolves it to a fixed end date.
  await page.getByRole('button', { name: 'Efter antal' }).click()
  await page.getByRole('textbox', { name: 'Antal gånger' }).fill('3')

  await page.getByRole('button', { name: 'Sätt på repeat' }).click()
  await expect(page.getByText(/bokförs först/)).toBeVisible() // creation toast

  // Lands on /recurring; the detail sheet surfaces the resolved end date ("· slutar <datum>").
  const card = page.locator('[data-slot="card"]', { hasText: title })
  await expect(card).toBeVisible()
  await card.getByRole('button', { name: 'Detaljer' }).click()
  await expect(page.getByText(/slutar/)).toBeVisible()
})

// The "Datum" mode requires an actual date before saving (mirrors the API 400).
test('blocks save when end mode is Datum but no date is chosen', async ({ page }) => {
  await loginAs(page, 'Du')
  const household = await getHouseholdId(page.request, 'Lönnvägen 3')
  await pinHousehold(page, household)
  await page.goto('/')

  await openAddSheet(page)
  await page.getByRole('tab', { name: 'Återkommande' }).click()
  await page.getByRole('textbox', { name: 'Belopp' }).fill('120')
  await page.getByRole('button', { name: 'Datum' }).click()

  await page.getByRole('button', { name: 'Sätt på repeat' }).click()
  await expect(page.getByText('Välj ett slutdatum')).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Ny post' })).toBeVisible()
})
