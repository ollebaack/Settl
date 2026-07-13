/**
 * Shared e2e helpers. The specs run against the REAL stack (seeded e2e Postgres
 * database, live .NET API on :5000, Vite on :5173). The DB is a single shared
 * store across parallel workers + both projects, so every spec either (a)
 * creates its own uniquely-named data, or (b) round-trips a toggle back to its
 * original state.
 *
 * Auth is real (ADR-0011): `loginAs` logs a seeded member in via the actual
 * cookie session, using `page.request` so the cookie lands in the page's own
 * browser context — subsequent `page.goto()` navigations and `page.request.*`
 * setup calls are then authenticated as that member. Test data is created via
 * direct API calls (the same contract the SPA uses).
 */
import { type APIRequestContext, type Page, expect } from '@playwright/test'

export const API = 'http://localhost:5000'

/** Shared dev password for every seeded member — apps/api/Settl.Api/Data/SeedIds.cs. */
const DEV_PASSWORD = 'Settl-Dev-123!'

export interface Member {
  id: string
  name: string
  avatarColor: string
}

function emailFor(name: string): string {
  return `${name.toLowerCase()}@settl.dev`
}

/** A unique suffix so parallel workers / both projects never collide on data. */
export function uniqueSuffix(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

/**
 * Logs in as a seeded member via the real cookie session. Run this before
 * page.goto and before any page.request.* setup calls that need auth. Returns
 * the member's id.
 */
export async function loginAs(page: Page, name: string): Promise<string> {
  const res = await page.request.post(`${API}/auth/login`, {
    data: { email: emailFor(name), password: DEV_PASSWORD },
  })
  expect(res.ok(), `login as ${name}`).toBeTruthy()
  return ((await res.json()) as Member).id
}

/** Resolve a household id by name for the logged-in member (call after loginAs). */
export async function getHouseholdId(request: APIRequestContext, name: string): Promise<string> {
  const res = await request.get(`${API}/households`)
  expect(res.ok(), 'GET /households').toBeTruthy()
  const list = (await res.json()) as Array<{ id: string; name: string }>
  const found = list.find((h) => h.name === name)
  if (!found) throw new Error(`Household "${name}" not found`)
  return found.id
}

export async function getMembers(
  request: APIRequestContext,
  householdId: string,
): Promise<Member[]> {
  const res = await request.get(`${API}/households/${householdId}/members`)
  expect(res.ok(), 'GET members').toBeTruthy()
  return (await res.json()) as Member[]
}

/**
 * Pins the active household in localStorage so the app renders deterministic
 * content regardless of default ordering. Must run before goto.
 */
export async function pinHousehold(page: Page, householdId: string): Promise<void> {
  await page.addInitScript((h) => {
    localStorage.setItem('settl.activeHouseholdId', h)
  }, householdId)
}

// --- API data factories -----------------------------------------------------

export async function createExpense(
  request: APIRequestContext,
  householdId: string,
  title: string,
  amountMinor: number,
  paidByMemberId: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/entries`, {
    data: {
      type: 'expense',
      title,
      amountMinor,
      date: null,
      paidByMemberId,
      fromMemberId: null,
      toMemberId: null,
      split: { mode: 'equal', values: null },
    },
  })
  expect(res.ok(), `create expense: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
}

export async function createIou(
  request: APIRequestContext,
  householdId: string,
  title: string,
  amountMinor: number,
  fromMemberId: string,
  toMemberId: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/entries`, {
    data: {
      type: 'iou',
      title,
      amountMinor,
      date: null,
      paidByMemberId: null,
      fromMemberId,
      toMemberId,
      split: null,
    },
  })
  expect(res.ok(), `create iou: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
}

export async function createRecurring(
  request: APIRequestContext,
  householdId: string,
  title: string,
  amountMinor: number,
  nextPostDate: string,
  paidByMemberId: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/recurring`, {
    data: {
      title,
      amountMinor,
      cadence: 'monthly',
      nextPostDate,
      paidByMemberId,
      split: { mode: 'equal', values: null },
    },
  })
  expect(res.ok(), `create recurring: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
}

export async function createHousehold(
  request: APIRequestContext,
  name: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households`, {
    data: { name, currency: 'SEK' },
  })
  expect(res.ok(), `create household: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
}

/** Reads the most recent invite's accept token via the Development-only dev
 * side channel (the raw token is never persisted or returned any other way —
 * this is what a real inbox would have given the invitee). */
export async function latestDevInviteToken(request: APIRequestContext): Promise<string> {
  const res = await request.get(`${API}/dev/invites/latest`)
  expect(res.ok(), 'GET /dev/invites/latest').toBeTruthy()
  const { acceptUrl } = (await res.json()) as { acceptUrl: string }
  return new URL(acceptUrl).searchParams.get('token')!
}

/** Reads the most recent email-verification link via the Development-only dev side channel
 * (same reasoning as latestDevInviteToken — a real inbox is the only other way to get it). */
export async function latestDevVerificationUrl(request: APIRequestContext): Promise<string> {
  const res = await request.get(`${API}/dev/verifications/latest`)
  expect(res.ok(), 'GET /dev/verifications/latest').toBeTruthy()
  return ((await res.json()) as { confirmUrl: string }).confirmUrl
}

/** Reads the most recent password-reset link via the Development-only dev side channel. */
export async function latestDevPasswordResetUrl(request: APIRequestContext): Promise<string> {
  const res = await request.get(`${API}/dev/password-resets/latest`)
  expect(res.ok(), 'GET /dev/password-resets/latest').toBeTruthy()
  return ((await res.json()) as { resetUrl: string }).resetUrl
}

/** Converts an absolute dev-emailed link (Web:BaseUrl + path) into a same-origin relative
 * path + query so it can be passed straight to page.goto. */
export function relativePath(url: string): string {
  const u = new URL(url)
  return `${u.pathname}${u.search}`
}

/** Sends a household invite and returns its accept token. */
export async function inviteAndGetToken(
  request: APIRequestContext,
  householdId: string,
  email: string,
): Promise<string> {
  const res = await request.post(`${API}/households/${householdId}/invites`, {
    data: { email },
  })
  expect(res.ok(), `create invite: ${await safeText(res)}`).toBeTruthy()
  return latestDevInviteToken(request)
}

/** Accepts an invite as whoever `request`'s browser context is currently logged in as
 * (or creates+signs in a brand-new account when `password` is given for a new email). */
export async function acceptInvite(
  request: APIRequestContext,
  token: string,
  body: { name?: string | null; password?: string | null } = {},
): Promise<void> {
  const res = await request.post(`${API}/invites/${token}/accept`, {
    data: { name: body.name ?? null, password: body.password ?? null },
  })
  expect(res.ok(), `accept invite: ${await safeText(res)}`).toBeTruthy()
}

async function safeText(res: { text: () => Promise<string> }): Promise<string> {
  try {
    return await res.text()
  } catch {
    return '<no body>'
  }
}

// --- Viewport-agnostic UI helpers -------------------------------------------

/**
 * Open the "Ny post" add sheet. The desktop sidebar button and the mobile FAB
 * both expose the accessible name "Ny post"; only one is visible per viewport,
 * so we click the visible one.
 */
export async function openAddSheet(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Ny post' }).filter({ visible: true }).first().click()
  await expect(page.getByRole('heading', { name: 'Ny post' })).toBeVisible()
}

/**
 * Open the "Dina hushåll" switcher via its trigger (desktop sidebar button /
 * mobile header pill), both of which carry the current household name.
 */
export async function openHouseholdSwitcher(page: Page, currentName: string): Promise<void> {
  await page
    .getByRole('button', { name: new RegExp(escapeRegExp(currentName)) })
    .filter({ visible: true })
    .first()
    .click()
  await expect(page.getByText('Dina hushåll')).toBeVisible()
}

function escapeRegExp(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}
