import { test, expect } from '@playwright/test'
import { getHouseholdId, loginAs, pinHousehold } from './helpers'

// LOGGBOK, now folded into the merged Hushållet page (adaptive-home spec): the old
// `/ledger` tab redirects to `/hushallet`, where the filterable, day-grouped log
// lives below the hero + balances. Assertions about entry presence are scoped to
// the main feed, because on the desktop shell the right rail also renders
// nudges/upcoming that mention entries.
test.describe('Loggbok (in the Hushållet page)', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Du')
    const household = await getHouseholdId(page.request, 'Lönnvägen 3')
    await pinHousehold(page, household)
    // The retired /ledger route redirects into the merged page.
    await page.goto('/ledger')
    await expect(page).toHaveURL(/\/hushallet$/)
    await expect(page.getByRole('button', { name: 'Alla', exact: true })).toBeVisible()
  })

  test('renders the filter pills, entries and day-group headers', async ({ page }) => {
    for (const label of ['Alla', 'Utgifter', 'Repeat']) {
      await expect(page.getByRole('button', { name: label, exact: true })).toBeVisible()
    }
    const feed = page.getByRole('main')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
    // Day-group headers are level-2 headings (Idag / Igår / "{d} {mån}").
    await expect(feed.getByRole('heading', { level: 2 }).first()).toBeVisible()
  })

  // Filter assertions key on "Begagnad soffa" — a one-off expense that never
  // appears under the "Repeat" filter and is never echoed by the mobile "På gång"
  // rail (which mirrors upcoming recurring titles in the main region on a
  // date-relative window). Recurring titles are therefore not date-stable to
  // assert on here.
  test('the "Repeat" filter hides one-off expenses', async ({ page }) => {
    const feed = page.getByRole('main')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()

    await page.getByRole('button', { name: 'Repeat', exact: true }).click()
    await expect(feed.getByText('Begagnad soffa')).toHaveCount(0)
  })

  test('the "Utgifter" filter keeps expenses in the log', async ({ page }) => {
    const feed = page.getByRole('main')
    await page.getByRole('button', { name: 'Utgifter', exact: true }).click()
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
  })

  // Search is a client-side text filter over the loaded log (title / category /
  // member names). "soffa" matches "Begagnad soffa" and little else.
  test('searching narrows the log to matching entries and clears back', async ({ page }) => {
    const feed = page.getByRole('main')
    const search = page.getByRole('textbox', { name: 'Sök i loggboken' })

    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
    await search.fill('soffa')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()

    // A query with no hits shows the search-specific empty copy.
    await search.fill('zzzzzzz')
    await expect(feed.getByText('Begagnad soffa')).toHaveCount(0)
    await expect(feed.getByText(/Inga träffar för/)).toBeVisible()

    // Clearing restores the full log.
    await page.getByRole('button', { name: 'Rensa sök' }).click()
    await expect(search).toHaveValue('')
    await expect(feed.getByText('Begagnad soffa')).toBeVisible()
  })
})
