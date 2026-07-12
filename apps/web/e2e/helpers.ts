/**
 * Shared e2e helpers. The specs run against the REAL stack (seeded e2e.db, live
 * .NET API on :5000, Vite on :5173). The DB is a single shared store across
 * parallel workers + both projects, so every spec either (a) creates its own
 * uniquely-named data, or (b) round-trips a toggle back to its original state.
 *
 * Test data is seeded/created via direct API calls (the same contract the SPA
 * uses) and the acting user is pinned via localStorage so the UI is deterministic.
 */
import { type APIRequestContext, type Page, expect } from '@playwright/test'

export const API = 'http://localhost:5000'

export interface Member {
  id: string
  name: string
  avatarColor: string
}

/** Auth header for the dev "acting user" (X-Settl-User; tech-debt 0003). */
export function auth(memberId: string) {
  return { headers: { 'X-Settl-User': memberId } }
}

/** A unique suffix so parallel workers / both projects never collide on data. */
export function uniqueSuffix(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

/** Resolve a seeded member id by name (GET /dev/users is unauthenticated). */
export async function getMemberId(request: APIRequestContext, name: string): Promise<string> {
  const res = await request.get(`${API}/dev/users`)
  expect(res.ok(), 'GET /dev/users').toBeTruthy()
  const users = (await res.json()) as Member[]
  const found = users.find((u) => u.name === name)
  if (!found) throw new Error(`Seed member "${name}" not found in ${JSON.stringify(users)}`)
  return found.id
}

/** Resolve a household id by name for the acting user. */
export async function getHouseholdId(
  request: APIRequestContext,
  memberId: string,
  name: string,
): Promise<string> {
  const res = await request.get(`${API}/households`, auth(memberId))
  expect(res.ok(), 'GET /households').toBeTruthy()
  const list = (await res.json()) as Array<{ id: string; name: string }>
  const found = list.find((h) => h.name === name)
  if (!found) throw new Error(`Household "${name}" not found`)
  return found.id
}

export async function getMembers(
  request: APIRequestContext,
  memberId: string,
  householdId: string,
): Promise<Member[]> {
  const res = await request.get(`${API}/households/${householdId}/members`, auth(memberId))
  expect(res.ok(), 'GET members').toBeTruthy()
  return (await res.json()) as Member[]
}

/**
 * Pin the dev acting user + active household in localStorage so the app renders
 * deterministic content regardless of default ordering. Must run before goto.
 */
export async function pin(page: Page, memberId: string, householdId: string): Promise<void> {
  await page.addInitScript(
    ([m, h]) => {
      localStorage.setItem('settl.currentMemberId', m)
      localStorage.setItem('settl.activeHouseholdId', h)
    },
    [memberId, householdId] as [string, string],
  )
}

// --- API data factories -----------------------------------------------------

export async function createExpense(
  request: APIRequestContext,
  memberId: string,
  householdId: string,
  title: string,
  amountMinor: number,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/entries`, {
    ...auth(memberId),
    data: {
      type: 'expense',
      title,
      amountMinor,
      date: null,
      paidByMemberId: memberId,
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
  actingMemberId: string,
  householdId: string,
  title: string,
  amountMinor: number,
  fromMemberId: string,
  toMemberId: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/entries`, {
    ...auth(actingMemberId),
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
  memberId: string,
  householdId: string,
  title: string,
  amountMinor: number,
  nextPostDate: string,
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households/${householdId}/recurring`, {
    ...auth(memberId),
    data: {
      title,
      amountMinor,
      cadence: 'monthly',
      nextPostDate,
      paidByMemberId: memberId,
      split: { mode: 'equal', values: null },
    },
  })
  expect(res.ok(), `create recurring: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
}

export async function createHousehold(
  request: APIRequestContext,
  memberId: string,
  name: string,
  memberIds: string[],
): Promise<{ id: string }> {
  const res = await request.post(`${API}/households`, {
    ...auth(memberId),
    data: { name, memberIds, currency: 'SEK' },
  })
  expect(res.ok(), `create household: ${await safeText(res)}`).toBeTruthy()
  return (await res.json()) as { id: string }
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
