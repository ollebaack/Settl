/**
 * Typed fetch layer over the generated OpenAPI contract (@settl/api-client).
 * Every request carries the ASP.NET Identity auth cookie (ADR-0011); the API
 * resolves the acting member from it, never from anything the client sends.
 * Non-2xx responses are parsed as ProblemDetails and thrown as an Error carrying
 * the Swedish `detail` message so screens can surface it.
 * Response/request DTO types are re-exported here so screens import from one place.
 */
import type { components, paths } from '@settl/api-client'
import { recordRequest } from './dev-bar-store'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(method: Method, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}

  const init: RequestInit = { method, headers, credentials: 'include' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    init.body = JSON.stringify(body)
  }

  const startedAt = performance.now()
  let status: number | 'error' = 'error'

  try {
    const response = await fetch(`${API_BASE_URL}${path}`, init)
    status = response.status

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
  } finally {
    recordRequest({
      method,
      path,
      status,
      durationMs: Math.round(performance.now() - startedAt),
      timestamp: new Date().toISOString(),
    })
  }
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
export type RemovalPreviewDto = Schemas['RemovalPreviewDto']
export type LeaveResultDto = Schemas['LeaveResultDto']
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
export type SettlementHistoryItemDto = Schemas['SettlementHistoryItemDto']
export type SettlementHistoryEntryDto = Schemas['SettlementHistoryEntryDto']
export type NudgeDto = Schemas['NudgeDto']
export type NudgeActionDto = Schemas['NudgeActionDto']
export type CreateSettlementResponse = Schemas['CreateSettlementResponse']
export type ProblemDetails = Schemas['ProblemDetails']
export type InviteDto = Schemas['InviteDto']
export type InvitePreviewDto = Schemas['InvitePreviewDto']
export type MeDto = Schemas['MeDto']
export type ContactDto = Schemas['ContactDto']
export type ContactInviteResultDto = Schemas['ContactInviteResultDto']
export type PendingInviteDto = Schemas['PendingInviteDto']
export type InvitableContactDto = Schemas['InvitableContactDto']

// Request DTOs
export type CreateEntryRequest = Schemas['CreateEntryRequest']
export type UpdateEntryRequest = Schemas['UpdateEntryRequest']
export type CreateHouseholdRequest = Schemas['CreateHouseholdRequest']
export type TransferOwnershipRequest = Schemas['TransferOwnershipRequest']
export type CreateRecurringRequest = Schemas['CreateRecurringRequest']
export type UpdateRecurringRequest = Schemas['UpdateRecurringRequest']
export type CreateSettlementRequest = Schemas['CreateSettlementRequest']
export type SplitInput = Schemas['SplitInput']
export type RegisterRequest = Schemas['RegisterRequest']
export type LoginRequest = Schemas['LoginRequest']
export type CreateInviteRequest = Schemas['CreateInviteRequest']
export type CreateContactInviteRequest = Schemas['CreateContactInviteRequest']
export type InviteContactRequest = Schemas['InviteContactRequest']
export type UpdateProfileRequest = Schemas['UpdateProfileRequest']
export type AcceptInviteRequest = Schemas['AcceptInviteRequest']
export type ConfirmEmailRequest = Schemas['ConfirmEmailRequest']
export type ForgotPasswordRequest = Schemas['ForgotPasswordRequest']
export type ResetPasswordRequest = Schemas['ResetPasswordRequest']
export type UpdateMeRequest = Schemas['UpdateMeRequest']

// Domain string-union helpers (the API serialises these as plain strings)
export type EntryType = 'expense' | 'recurringPost'
export type EntryCategory =
  | 'cleaning'
  | 'restaurant'
  | 'event'
  | 'furniture'
  | 'groceries'
  | 'transport'
  | 'internet'
  | 'rent'
  | 'music'
  | 'streaming'
  | 'electricity'
  | 'gift'
  | 'other'
export type SplitModeName = 'equal' | 'percent' | 'amount' | 'none'
export type ViewerStatusKind =
  | 'settled'
  | 'youOwe'
  | 'youAreOwed'
  | 'partiallySettled'
  | 'notYourShare'
export type NudgeActionKind = 'viewEntry' | 'viewRecurring' | 'settle'
export type EntryFilter = 'all' | 'expense' | 'recurring'
export type NudgeTone = 'gentle' | 'direct'
export type InviteChannel = 'sms' | 'email'
/** invitable-contacts status: already joined | pick to invite | invite already outstanding */
export type ContactStatus = 'member' | 'invitable' | 'pending'
