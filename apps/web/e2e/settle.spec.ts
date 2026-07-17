import { test, expect } from '@playwright/test'
import {
  API,
  acceptInvite,
  createHousehold,
  createOneOwesAll,
  getMembers,
  inviteAndGetToken,
  loginAs,
  pinHousehold,
  uniqueSuffix,
} from './helpers'

// SETTLE-UP (Gör upp, §2.8 + flow §4). Runs in a throwaway household created per
// test invocation so settling the whole pair cannot disturb the seeded books
// that the other specs assert against (fully isolated; no restore needed).
// Sam joins via a real invite (ADR-0011 — households are invite-only), so the
// login switches Du → Sam → Du before the actual UI test runs as Du.
test('settle up with a person closes the pair', async ({ page }) => {
  const du = await loginAs(page, 'Du')

  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const household = await createHousehold(page.request, `E2E Gör upp ${suffix}`)
  const token = await inviteAndGetToken(page.request, household.id, 'sam@settl.dev')

  const sam = await loginAs(page, 'Sam')
  await acceptInvite(page.request, token)
  await loginAs(page, 'Du')

  // Members are ordered by join order; find Sam in the new book.
  const members = await getMembers(page.request, household.id)
  const samMember = members.find((m) => m.id === sam)!

  // Sam owes Du 500 kr → on Home this shows as a non-square person row.
  await createOneOwesAll(page.request, household.id, 'E2E skuld', 50000, sam, du)

  await pinHousehold(page, household.id)
  // "Du" has several books, so `/` is the overview; the person rows live on the
  // focused book dashboard (ADR-0019).
  await page.goto(`/hushall/${household.id}`)

  // Open settle-up from the person row.
  await page.getByTestId('person-row').first().click()

  const sheet = page.getByLabel(`Gör upp med ${samMember.name}`)
  await expect(page.getByRole('heading', { name: `Gör upp med ${samMember.name}` })).toBeVisible()
  await expect(sheet.getByText(`${samMember.name} är skyldig dig`)).toBeVisible()
  await expect(sheet.getByText('E2E skuld')).toBeVisible()

  await page.getByRole('button', { name: 'Markera allt som reglerat' }).click()
  await expect(page.getByText(`Uppgjort med ${samMember.name} — rent bord`)).toBeVisible()
})

// ADD-YOUR-NUMBER NUDGE (ADR-0026). The flip side of Swish pay: when you are OWED by someone and
// have no number saved, the debtor can't Swish you — so the settle-up sheet nudges you to add one,
// deep-linking to the single number field on /profil. Isolated throwaway book; Du has no number.
test('nudges the creditor to add a number, linking to profile', async ({ page }) => {
  const du = await loginAs(page, 'Du')

  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const household = await createHousehold(page.request, `E2E Nudge ${suffix}`)
  const token = await inviteAndGetToken(page.request, household.id, 'sam@settl.dev')

  const sam = await loginAs(page, 'Sam')
  await acceptInvite(page.request, token)
  await loginAs(page, 'Du')

  const members = await getMembers(page.request, household.id)
  const samMember = members.find((m) => m.id === sam)!

  // Sam owes Du → Du is the creditor (owesYou) with no number saved.
  await createOneOwesAll(page.request, household.id, 'E2E nudge-skuld', 50000, sam, du)

  await pinHousehold(page, household.id)
  await page.goto(`/hushall/${household.id}`)
  await page.getByTestId('person-row').first().click()

  const sheet = page.getByLabel(`Gör upp med ${samMember.name}`)
  await expect(sheet.getByText(`${samMember.name} är skyldig dig`)).toBeVisible()

  // The creditor-side nudge, and tapping it lands on the profile number field.
  await sheet.getByRole('button', { name: 'Lägg till nummer' }).click()
  await expect(page).toHaveURL((url) => url.pathname === '/profil')
  await expect(page.locator('#profile-phone')).toBeVisible()
})

// HOME "ADD YOUR NUMBER" BANNER (ADR-0026). On the overview, when you're owed money in a book and
// have no number saved, a dismissible banner invites you to add one so people can Swish you back.
// It links to /profil and dismissing it removes it. Isolated throwaway book; Du has no number.
test('home banner invites the owed user to add a number, and dismisses', async ({ page }) => {
  const du = await loginAs(page, 'Du')

  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const household = await createHousehold(page.request, `E2E Banner ${suffix}`)
  const token = await inviteAndGetToken(page.request, household.id, 'sam@settl.dev')

  const sam = await loginAs(page, 'Sam')
  await acceptInvite(page.request, token)
  await loginAs(page, 'Du')

  // Sam owes Du → Du is owed in at least one book (netLabel 'owed').
  await createOneOwesAll(page.request, household.id, 'E2E banner-skuld', 50000, sam, du)

  await page.goto('/')
  const banner = page.getByText('Lägg till ditt nummer').first()
  await expect(banner).toBeVisible()

  // Dismissing removes it and it stays gone across a reload (persisted, never nags).
  await page.getByRole('button', { name: 'Dölj' }).click()
  await expect(page.getByText('Lägg till ditt nummer')).toHaveCount(0)
  await page.reload()
  await expect(page.getByText('Lägg till ditt nummer')).toHaveCount(0)
})

// SWISH PAY (swish-settlement-payments spec). When the acting user OWES someone in a SEK book
// and that creditor has saved a number, the settle-up sheet offers a "Betala med Swish"
// launcher next to "mark settled". Fully isolated: a throwaway book + a freshly-invited creditor
// account whose number is set via the API (ADR-0026 — the single number doubles as the Swish
// payee), so no seeded member is mutated.
test('offers Betala med Swish when the creditor has a number', async ({ page }) => {
  const du = await loginAs(page, 'Du')

  const suffix = `${test.info().project.name}-${uniqueSuffix()}`
  const household = await createHousehold(page.request, `E2E Swish ${suffix}`)

  // Invite + accept a brand-new creditor account (email owner), signing page.request in as them.
  const creditorEmail = `e2e-swish-payee-${suffix}@example.com`
  const token = await inviteAndGetToken(page.request, household.id, creditorEmail)
  await acceptInvite(page.request, token, { name: 'Swishmottagare', password: 'Password123!' })

  // As the creditor, save a number (PUT /me is AuthenticatedOnly) — it powers the Swish payee.
  const putPhone = await page.request.put(`${API}/me`, {
    data: { name: 'Swishmottagare', avatarEmoji: null, phone: '0701234567' },
  })
  expect(putPhone.ok(), 'creditor saves number').toBeTruthy()

  await loginAs(page, 'Du')
  const members = await getMembers(page.request, household.id)
  const creditor = members.find((m) => m.id !== du)!

  // Du owes the creditor 300 kr → Du is the debtor, so the pay launcher should appear.
  await createOneOwesAll(page.request, household.id, 'E2E Swish-skuld', 30000, du, creditor.id)

  await pinHousehold(page, household.id)
  await page.goto(`/hushall/${household.id}`)
  await page.getByTestId('person-row').first().click()

  const sheet = page.getByLabel(`Gör upp med ${creditor.name}`)
  await expect(page.getByRole('heading', { name: `Gör upp med ${creditor.name}` })).toBeVisible()
  await expect(sheet.getByText(`Du är skyldig ${creditor.name}`)).toBeVisible()
  // Viewport-agnostic: the mobile tap-link and the desktop QR block both carry this label.
  await expect(sheet.getByText('Betala med Swish')).toBeVisible()
})
