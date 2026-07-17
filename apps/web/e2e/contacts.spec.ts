import { test, expect } from '@playwright/test'
import { API, latestDevSmsInviteToken, loginAs, uniqueSuffix } from './helpers'

// CONTACTS & BLIND SMS INVITES (ADR-0019). Adds a contact by phone number from /kontakter —
// which sends a blind SMS invite that reveals nothing about registration — then accepts it as a
// brand-new account and confirms the reciprocal contact edge. Uses unique data (phone, email,
// name) so it never collides with the shared seeded e2e database, and does the profile-phone
// check as the freshly-created invitee (a unique account) to avoid mutating shared seed members.
test('add a contact by phone, accept the SMS invite, and see the connection', async ({ page }) => {
  await loginAs(page, 'Du')

  const suffix = uniqueSuffix()
  // A unique Swedish subscriber number → E.164 +4670XXXXXXX server-side.
  const local = `70${String(Date.now()).slice(-7)}`
  const e164 = `+46${local}`

  await page.goto('/kontakter')
  await expect(page.getByRole('heading', { name: 'Kontakter' })).toBeVisible()

  // Open the add-a-friend sheet. SMS is the default channel; the privacy note is explicit.
  await page.getByRole('button', { name: '+ Lägg till kontakt' }).click()
  await expect(page.getByRole('heading', { name: 'Lägg till kontakt' })).toBeVisible()
  await expect(page.getByText(/Vi berättar inte om numret redan använder Settl/)).toBeVisible()

  await page.getByRole('textbox', { name: 'Telefonnummer' }).fill(local)
  await page.getByRole('button', { name: 'Skicka SMS-inbjudan' }).click()

  // Blind confirmation — no "user exists" branch.
  await expect(page.getByRole('heading', { name: 'Inbjudan skickad' })).toBeVisible()
  await page.getByRole('button', { name: 'Klart' }).click()
  // Wait for the sheet to fully close (its info box also mentions "Väntar på svar").
  await expect(page.getByRole('heading', { name: 'Inbjudan skickad' })).toBeHidden()

  // The number now sits under "Väntar på svar" as the raw number, not a person.
  await expect(page.getByText('Väntar på svar', { exact: true })).toBeVisible()
  await expect(page.getByText(e164)).toBeVisible()

  // Accept the blind invite as a brand-new person (their own email becomes the identity).
  // Clear Du's session first: a logged-in user would instead accept as themselves.
  const token = await latestDevSmsInviteToken(page.request, e164)
  const inviteeName = `Kontakt ${suffix}`
  await page.context().clearCookies()
  await page.goto(`/accept-invite?token=${token}`)
  await expect(page.getByText('vill lägga till dig')).toBeVisible()
  await page.getByLabel('Ditt namn').fill(inviteeName)
  await page.getByLabel('E-post').fill(`contact-${suffix}@example.com`)
  await page.getByLabel('Lösenord').fill('Password123!')
  await page.getByRole('button', { name: 'Skapa konto & fortsätt' }).click()
  // Signed in and landed home (path only — a brand-new invitee has no household, so
  // home immediately appends its onboarding query; pinning the full URL races that).
  await expect(page).toHaveURL((url) => url.pathname === '/')

  // Now signed in as the invitee — Du appears in their contacts (connection-on-accept).
  await page.goto('/kontakter')
  await expect(page.getByText('Du', { exact: true }).first()).toBeVisible()

  // And the edge is reciprocal: Du has the new person too (checked via the API contract).
  const theirContacts = await page.request.get(`${API}/contacts`)
  expect(theirContacts.ok()).toBeTruthy()
  const list = (await theirContacts.json()) as Array<{ name: string }>
  expect(list.some((c) => c.name === 'Du')).toBeTruthy()

  // Save an optional, unverified profile phone (this invitee is unique, so no shared-state race).
  const myLocal = `73${String(Date.now()).slice(-7)}`
  await expect(page.getByText('Overifierat')).toBeVisible()
  await page.getByRole('textbox', { name: 'Ditt telefonnummer' }).fill(myLocal)
  // Gate the reload on the save landing server-side. The "Nummer sparat" toast
  // auto-dismisses within seconds and can't be reliably caught under CI load, and
  // reloading before the PATCH resolves would drop the write. Persistence across
  // the reload is the durable proof the number saved.
  const saved = page.waitForResponse(
    (r) => r.url().endsWith('/me') && r.request().method() === 'PATCH' && r.ok(),
  )
  await page.getByRole('button', { name: 'Spara' }).click()
  await saved
  await page.reload()
  await expect(page.getByRole('textbox', { name: 'Ditt telefonnummer' })).toHaveValue(myLocal)
})
