import { test, expect } from '@playwright/test'
import { createRecurring, getHouseholdId, getMemberId, pin, uniqueSuffix } from './helpers'

// RECURRING (På repeat, §2.3 + flow §4): the screen lists templates with cycle
// progress; pause then resume a template (round-trip restores state). Uses a
// freshly-created, uniquely-titled template scheduled far out (so it adds no
// due-nudge) to stay isolated from the other specs.
test('pause and resume a recurring template (round-trip)', async ({ page, request }) => {
  const du = await getMemberId(request, 'Du')
  const household = await getHouseholdId(request, du, 'Lönnvägen 3')

  const title = `E2E repeat ${uniqueSuffix()}`
  const far = new Date(Date.now() + 40 * 86_400_000).toISOString().slice(0, 10)
  await createRecurring(request, du, household, title, 30000, far)

  await pin(page, du, household)
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
