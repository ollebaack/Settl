/**
 * Typed fetch layer over the generated OpenAPI contract (@settl/api-client).
 * Every request carries the ASP.NET Identity auth cookie (ADR-0011); the API
 * resolves the acting member from it, never from anything the client sends.
 * Non-2xx responses are parsed as ProblemDetails and thrown as an Error carrying
 * the Swedish `detail` message so screens can surface it.
 * Response/request DTO types are re-exported here so screens import from one place.
 */
import type { components, paths } from '@settl/api-client'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(method: Method, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}

  const init: RequestInit = { method, headers, credentials: 'include' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(body)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, init)

  if (!response.ok) {
    let detail: string | undefined
    try {
      const problem = (await response.json()) as ProblemDetails
      detail = problem?.detail ?? problem?.title ?? undefined
    } catch {
      /* body was not ProblemDetails JSON */
    }
    throw new Error(detail ?? `API ${response.status} ${method} ${path}`)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export const apiGet = <T>(path: string) => request<T>('GET', path)
export const apiPost = <T>(path: string, body?: unknown) => request<T>('POST', path, body)
export const apiPut = <T>(path: string, body?: unknown) => request<T>('PUT', path, body)
export const apiPatch = <T>(path: string, body?: unknown) => request<T>('PATCH', path, body)
export const apiDelete = <T>(path: string, body?: unknown) => request<T>('DELETE', path, body)

/** Escape hatch for advanced typing; prefer the named DTO aliases below. */
export type { components, paths }

type Schemas = components['schemas']

// Response DTOs
export type MemberDto = Schemas['MemberDto']
export type HouseholdDto = Schemas['HouseholdDto']
export type HouseholdListItemDto = Schemas['HouseholdListItemDto']
export type HouseholdSummaryDto = Schemas['HouseholdSummaryDto']
export type PersonBalanceDto = Schemas['PersonBalanceDto']
export type UpcomingDto = Schemas['UpcomingDto']
export type EntryDto = Schemas['EntryDto']
export type ShareDto = Schemas['ShareDto']
export type ViewerStatusDto = Schemas['ViewerStatusDto']
export type RecurringDto = Schemas['RecurringDto']
export type RecurringListDto = Schemas['RecurringListDto']
export type RecurringDetailDto = Schemas['RecurringDetailDto']
export type RecurringShareRowDto = Schemas['RecurringShareRowDto']
export type PostedEntrySummaryDto = Schemas['PostedEntrySummaryDto']
export type SettlePreviewDto = Schemas['SettlePreviewDto']
export type SettleEntryDto = Schemas['SettleEntryDto']
export type NudgeDto = Schemas['NudgeDto']
export type NudgeActionDto = Schemas['NudgeActionDto']
export type CreateSettlementResponse = Schemas['CreateSettlementResponse']
export type ProblemDetails = Schemas['ProblemDetails']
export type InviteDto = Schemas['InviteDto']
export type InvitePreviewDto = Schemas['InvitePreviewDto']

// Request DTOs
export type CreateEntryRequest = Schemas['CreateEntryRequest']
export type UpdateEntryRequest = Schemas['UpdateEntryRequest']
export type CreateHouseholdRequest = Schemas['CreateHouseholdRequest']
export type CreateRecurringRequest = Schemas['CreateRecurringRequest']
export type UpdateRecurringRequest = Schemas['UpdateRecurringRequest']
export type CreateSettlementRequest = Schemas['CreateSettlementRequest']
export type SplitInput = Schemas['SplitInput']
export type RegisterRequest = Schemas['RegisterRequest']
export type LoginRequest = Schemas['LoginRequest']
export type CreateInviteRequest = Schemas['CreateInviteRequest']
export type AcceptInviteRequest = Schemas['AcceptInviteRequest']

// Domain string-union helpers (the API serialises these as plain strings)
export type EntryType = 'expense' | 'iou' | 'recurringPost'
export type SplitModeName = 'equal' | 'percent' | 'amount' | 'none'
export type ViewerStatusKind =
  | 'settled'
  | 'youOwe'
  | 'youAreOwed'
  | 'partiallySettled'
  | 'notYourShare'
export type NudgeActionKind = 'viewEntry' | 'viewRecurring' | 'settle'
export type EntryFilter = 'all' | 'expense' | 'iou' | 'recurring'
export type NudgeTone = 'gentle' | 'direct'
