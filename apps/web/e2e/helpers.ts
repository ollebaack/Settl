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

/** API origin the specs hit directly. Defaults to the standard local API, but
 * can be pointed elsewhere (e.g. an isolated instance on another port) via
 * E2E_API_URL — kept in sync with playwright.config's webServer. */
export const API = process.env.E2E_API_URL ?? 'http://localhost:5000'

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

/**
 * One person owes the whole amount (the "Allt på en" split; ADR-0021 removed the
 * separate IOU type). `owerMemberId` owes `payerMemberId` the full amount — the payer
 * fronted it and takes a zero share.
 */
export async function createOneOwesAll(
  request: APIRequestContext,
  householdId: string,
  title: string,
  amountMinor: number,
  owerMemberId: string,
  payerMemberId: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/entries`, {
    data: {
      type: 'expense',
      title,
      amountMinor,
      date: null,
      paidByMemberId: payerMemberId,
      split: { mode: 'amount', values: { [owerMemberId]: amountMinor } },
    },
  })
  expect(res.ok(), `create one-owes-all: ${await safeText(res)}`).toBeTruthy()
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
  currency = 'SEK',
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households`, {
    data: { name, currency },
  })
  expect(res.ok(), `create household: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
}

/**
 * Registers a brand-new account and confirms its email via the Development-only
 * dev side channel, leaving `page.request` signed in as that (now confirmed)
 * member — ready to create households and load the app past RequireAuth. Polls
 * the "latest verification" channel until it sees THIS user's link, so parallel
 * signups don't hand us someone else's token.
 */
export async function registerConfirmedUser(
  page: Page,
  user: { name: string; email: string; password: string },
): Promise<string> {
  const res = await page.request.post(`${API}/auth/register`, { data: user })
  expect(res.ok(), `register ${user.email}: ${await safeText(res)}`).toBeTruthy()
  const id = ((await res.json()) as Member).id

  const confirmUrl = await devVerificationUrlFor(page.request, id)
  const token = new URL(confirmUrl).searchParams.get('token')
  const confirm = await page.request.post(`${API}/auth/confirm-email`, {
    data: { userId: id, token },
  })
  expect(confirm.ok(), `confirm ${user.email}: ${await safeText(confirm)}`).toBeTruthy()
  return id
}

/** Reads the most recent invite's accept token via the Development-only dev
 * side channel (the raw token is never persisted or returned any other way —
 * this is what a real inbox would have given the invitee). */
export async function latestDevInviteToken(
  request: APIRequestContext,
  email?: string,
): Promise<string> {
  // Scoping by `email` makes this robust to parallel workers: the dev channel indexes invites
  // by recipient, so a competing signup can't evict ours before we read it. Poll briefly since
  // the send is async after the UI action.
  const query = email ? `?email=${encodeURIComponent(email)}` : ''
  for (let attempt = 0; attempt < 20; attempt++) {
    const res = await request.get(`${API}/dev/invites/latest${query}`)
    if (res.ok()) {
      const { acceptUrl } = (await res.json()) as { acceptUrl: string }
      return new URL(acceptUrl).searchParams.get('token')!
    }
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  throw new Error(`No invite link surfaced${email ? ` for ${email}` : ''}`)
}

/** SMS equivalent of latestDevInviteToken (ADR-0019). Scope by `phone` (E.164) to avoid the
 * parallel-worker race; the raw token is never persisted any other way. */
export async function latestDevSmsInviteToken(
  request: APIRequestContext,
  phoneE164?: string,
): Promise<string> {
  const query = phoneE164 ? `?phone=${encodeURIComponent(phoneE164)}` : ''
  for (let attempt = 0; attempt < 20; attempt++) {
    const res = await request.get(`${API}/dev/sms-invites/latest${query}`)
    if (res.ok()) {
      const { acceptUrl } = (await res.json()) as { acceptUrl: string }
      return new URL(acceptUrl).searchParams.get('token')!
    }
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  throw new Error(`No SMS invite link surfaced${phoneE164 ? ` for ${phoneE164}` : ''}`)
}

/** Reads the most recent email-verification link via the Development-only dev side channel
 * (same reasoning as latestDevInviteToken — a real inbox is the only other way to get it). */
export async function latestDevVerificationUrl(request: APIRequestContext): Promise<string> {
  const res = await request.get(`${API}/dev/verifications/latest`)
  expect(res.ok(), 'GET /dev/verifications/latest').toBeTruthy()
  return ((await res.json()) as { confirmUrl: string }).confirmUrl
}

/**
 * Resolves THIS user's verification link, robust to parallel workers racing on the
 * single-slot dev channel. `/dev/verifications/latest` only ever holds the most recently
 * generated link, so a competing signup can evict ours before we read it — matching on
 * userId alone would then time out waiting for a link that's gone. So each round we
 * force-resend our own link (which re-publishes it into the slot) and read it straight
 * back, matching on userId in case a competitor still slipped in between. `authed` must be
 * a context signed in as `userId` (e.g. `page.request` right after signup).
 */
export async function devVerificationUrlFor(
  authed: APIRequestContext,
  userId: string,
): Promise<string> {
  for (let attempt = 0; attempt < 15; attempt++) {
    const resent = await authed.post(`${API}/auth/resend-verification`)
    expect(resent.ok(), `resend verification: ${await safeText(resent)}`).toBeTruthy()
    const confirmUrl = await latestDevVerificationUrl(authed)
    if (new URL(confirmUrl).searchParams.get('userId') === userId) return confirmUrl
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  throw new Error(`No verification link surfaced for user ${userId}`)
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
  return latestDevInviteToken(request, email)
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
  // Assert on the sheet's unique description — its "Dina hushåll" title now
  // collides with the multi-household overview's own heading (ADR-0019).
  await expect(page.getByText('En bok per hushåll — du kan vara med i flera.')).toBeVisible()
}

function escapeRegExp(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}
