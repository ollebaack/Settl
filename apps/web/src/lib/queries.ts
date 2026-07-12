/**
 * TanStack Query hooks + mutations for every endpoint in the API contract.
 * Server state only — no fetching in useEffect (see apps/web/CLAUDE.md).
 * Household-scoped reads are keyed by household id; mutations invalidate the
 * affected household's summary / entries / recurring / nudges as appropriate.
 */
import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from '@tanstack/react-query'
import {
  apiDelete,
  apiGet,
  apiPatch,
  apiPost,
  apiPut,
  type CreateEntryRequest,
  type CreateRecurringRequest,
  type CreateSettlementRequest,
  type CreateSettlementResponse,
  type EntryDto,
  type EntryFilter,
  type HouseholdDto,
  type HouseholdListItemDto,
  type HouseholdSummaryDto,
  type MemberDto,
  type NudgeDto,
  type NudgeTone,
  type RecurringDetailDto,
  type RecurringDto,
  type RecurringListDto,
  type SettlePreviewDto,
  type UpdateEntryRequest,
  type UpdateRecurringRequest,
} from './api'

// --- Query key factory ------------------------------------------------------

export interface EntriesParams {
  type?: EntryFilter
  limit?: number
}

export const queryKeys = {
  me: ['me'] as const,
  devUsers: ['dev-users'] as const,
  households: ['households'] as const,
  household: (id: string | undefined) => ['household', id] as const,
  members: (id: string | undefined) => ['household', id, 'members'] as const,
  summary: (id: string | undefined) => ['household', id, 'summary'] as const,
  entriesAll: (id: string | undefined) => ['household', id, 'entries'] as const,
  entries: (id: string | undefined, params?: EntriesParams) =>
    ['household', id, 'entries', params ?? {}] as const,
  entry: (entryId: string | undefined) => ['entry', entryId] as const,
  recurringList: (id: string | undefined) => ['household', id, 'recurring'] as const,
  recurringDetail: (recId: string | undefined) => ['recurring', recId] as const,
  settlePreviewAll: (id: string | undefined) => ['household', id, 'settle-preview'] as const,
  settlePreview: (id: string | undefined, person: string | undefined) =>
    ['household', id, 'settle-preview', person] as const,
  nudgesAll: (id: string | undefined) => ['household', id, 'nudges'] as const,
  nudges: (id: string | undefined, tone: NudgeTone) =>
    ['household', id, 'nudges', tone] as const,
}

/** Invalidate everything derived from a household's ledger state. */
export function invalidateHousehold(qc: QueryClient, householdId: string | undefined) {
  if (!householdId) return
  qc.invalidateQueries({ queryKey: queryKeys.summary(householdId) })
  qc.invalidateQueries({ queryKey: queryKeys.entriesAll(householdId) })
  qc.invalidateQueries({ queryKey: queryKeys.recurringList(householdId) })
  qc.invalidateQueries({ queryKey: queryKeys.nudgesAll(householdId) })
  qc.invalidateQueries({ queryKey: queryKeys.settlePreviewAll(householdId) })
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value))
  }
  const qs = search.toString()
  return qs ? `?${qs}` : ''
}

// --- Queries ----------------------------------------------------------------

export function useMe() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: () => apiGet<MemberDto>('/me'),
  })
}

export function useDevUsers() {
  return useQuery({
    queryKey: queryKeys.devUsers,
    queryFn: () => apiGet<MemberDto[]>('/dev/users'),
  })
}

export function useHouseholds() {
  return useQuery({
    queryKey: queryKeys.households,
    queryFn: () => apiGet<HouseholdListItemDto[]>('/households'),
  })
}

export function useHousehold(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.household(id),
    queryFn: () => apiGet<HouseholdDto>(`/households/${id}`),
    enabled: !!id,
  })
}

export function useMembers(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.members(id),
    queryFn: () => apiGet<MemberDto[]>(`/households/${id}/members`),
    enabled: !!id,
  })
}

export function useSummary(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.summary(id),
    queryFn: () => apiGet<HouseholdSummaryDto>(`/households/${id}/summary`),
    enabled: !!id,
  })
}

export function useEntries(id: string | undefined, params?: EntriesParams) {
  return useQuery({
    queryKey: queryKeys.entries(id, params),
    queryFn: () =>
      apiGet<EntryDto[]>(
        `/households/${id}/entries${buildQuery({
          type: params?.type === 'all' ? undefined : params?.type,
          limit: params?.limit,
          sort: 'date_desc',
        })}`,
      ),
    enabled: !!id,
  })
}

export function useEntry(entryId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.entry(entryId),
    queryFn: () => apiGet<EntryDto>(`/entries/${entryId}`),
    enabled: !!entryId,
  })
}

export function useRecurringList(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.recurringList(id),
    queryFn: () => apiGet<RecurringListDto>(`/households/${id}/recurring`),
    enabled: !!id,
  })
}

export function useRecurringDetail(recId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.recurringDetail(recId),
    queryFn: () => apiGet<RecurringDetailDto>(`/recurring/${recId}`),
    enabled: !!recId,
  })
}

export function useSettlePreview(id: string | undefined, person: string | undefined) {
  return useQuery({
    queryKey: queryKeys.settlePreview(id, person),
    queryFn: () =>
      apiGet<SettlePreviewDto>(
        `/households/${id}/settle-preview${buildQuery({ person })}`,
      ),
    enabled: !!id && !!person,
  })
}

export function useNudges(id: string | undefined, tone: NudgeTone = 'direct') {
  return useQuery({
    queryKey: queryKeys.nudges(id, tone),
    queryFn: () =>
      apiGet<NudgeDto[]>(`/households/${id}/nudges${buildQuery({ tone })}`),
    enabled: !!id,
  })
}

// --- Mutations --------------------------------------------------------------

export function useCreateEntry(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateEntryRequest) =>
      apiPost<EntryDto>(`/households/${householdId}/entries`, body),
    onSuccess: () => invalidateHousehold(qc, householdId),
  })
}

export function useUpdateEntry(householdId?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { entryId: string; body: UpdateEntryRequest }) =>
      apiPut<EntryDto>(`/entries/${vars.entryId}`, vars.body),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: queryKeys.entry(data.id) })
      invalidateHousehold(qc, householdId ?? data.householdId)
    },
  })
}

export function useDeleteEntry(householdId?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (entryId: string) => apiDelete<void>(`/entries/${entryId}`),
    onSuccess: (_data, entryId) => {
      qc.removeQueries({ queryKey: queryKeys.entry(entryId) })
      invalidateHousehold(qc, householdId)
    },
  })
}

export function useSettleEntry(householdId?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (entryId: string) =>
      apiPost<EntryDto>(`/entries/${entryId}/settlements`),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: queryKeys.entry(data.id) })
      invalidateHousehold(qc, householdId ?? data.householdId)
    },
  })
}

export function useReopenEntry(householdId?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (entryId: string) =>
      apiDelete<EntryDto>(`/entries/${entryId}/settlements`),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: queryKeys.entry(data.id) })
      invalidateHousehold(qc, householdId ?? data.householdId)
    },
  })
}

export function useCreateSettlement(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateSettlementRequest) =>
      apiPost<CreateSettlementResponse>(`/households/${householdId}/settlements`, body),
    onSuccess: () => invalidateHousehold(qc, householdId),
  })
}

export function useCreateRecurring(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateRecurringRequest) =>
      apiPost<RecurringDto>(`/households/${householdId}/recurring`, body),
    onSuccess: () => invalidateHousehold(qc, householdId),
  })
}

export function useUpdateRecurring(householdId?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (vars: { recId: string; body: UpdateRecurringRequest }) =>
      apiPatch<RecurringDto>(`/recurring/${vars.recId}`, vars.body),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: queryKeys.recurringDetail(data.id) })
      invalidateHousehold(qc, householdId)
    },
  })
}
