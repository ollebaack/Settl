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
  type AcceptInviteRequest,
  type ConfirmEmailRequest,
  type ContactDto,
  type ContactInviteResultDto,
  type CreateContactInviteRequest,
  type CreateEntryRequest,
  type CreateHouseholdRequest,
  type CreateInviteRequest,
  type InvitableContactDto,
  type InviteContactRequest,
  type CreateRecurringRequest,
  type CreateSettlementRequest,
  type CreateSettlementResponse,
  type EntryDto,
  type EntryFilter,
  type ForgotPasswordRequest,
  type HouseholdDto,
  type HouseholdListItemDto,
  type HouseholdSummaryDto,
  type InviteDto,
  type InvitePreviewDto,
  type LeaveResultDto,
  type LoginRequest,
  type MeDto,
  type MemberDto,
  type NudgeDto,
  type NudgeTone,
  type PendingInviteDto,
  type UpdateProfileRequest,
  type RecurringDetailDto,
  type RecurringDto,
  type RecurringListDto,
  type RegisterRequest,
  type RemovalPreviewDto,
  type ResetPasswordRequest,
  type SettlePreviewDto,
  type TransferOwnershipRequest,
  type UpdateEntryRequest,
  type UpdateMeRequest,
  type UpdateRecurringRequest,
} from './api'

// --- Query key factory ------------------------------------------------------

export interface EntriesParams {
  type?: EntryFilter
  limit?: number
}

export const queryKeys = {
  me: ['me'] as const,
  households: ['households'] as const,
  householdsWithArchived: ['households', 'withArchived'] as const,
  household: (id: string | undefined) => ['household', id] as const,
  members: (id: string | undefined) => ['household', id, 'members'] as const,
  removalPreview: (id: string | undefined) => ['household', id, 'removal-preview'] as const,
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
  householdInvites: (id: string | undefined) => ['household', id, 'invites'] as const,
  contacts: ['contacts'] as const,
  pendingContactInvites: ['contacts', 'pending'] as const,
  invitableContacts: (id: string | undefined) => ['household', id, 'invitable-contacts'] as const,
  invitePreview: (token: string | undefined) => ['invite', token] as const,
  confirmEmail: (userId: string | undefined, token: string | undefined) =>
    ['confirm-email', userId, token] as const,
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

/** 401 (not logged in) is a normal, expected state here — callers branch on `isError`,
 * not treat it as a failed fetch. */
export function useMe() {
  return useQuery({
    queryKey: queryKeys.me,
    queryFn: () => apiGet<MeDto>('/me'),
    retry: false,
  })
}

export function useInvitePreview(token: string | undefined) {
  return useQuery({
    queryKey: queryKeys.invitePreview(token),
    queryFn: () => apiGet<InvitePreviewDto>(`/invites/${token}`),
    enabled: !!token,
    retry: false,
  })
}

/** Fires the confirmation POST automatically once both params are present — modelled as a
 * query (like useInvitePreview) rather than a mutation triggered from an effect, so the page
 * just renders off `isPending`/`isError`/`data` with no manual fetch-on-mount wiring. */
export function useConfirmEmail(userId: string | undefined, token: string | undefined) {
  return useQuery({
    queryKey: queryKeys.confirmEmail(userId, token),
    // React Query rejects `undefined` as query data (apiPost<void> resolves to that for a
    // 204) — resolve to `true` instead so a successful confirmation still counts as data.
    queryFn: () =>
      apiPost<void>('/auth/confirm-email', { userId, token } as ConfirmEmailRequest).then(() => true),
    enabled: !!userId && !!token,
    retry: false,
  })
}

export function useHouseholdInvites(id: string | undefined) {
  return useQuery({
    queryKey: queryKeys.householdInvites(id),
    queryFn: () => apiGet<InviteDto[]>(`/households/${id}/invites`),
    enabled: !!id,
  })
}

export function useHouseholds() {
  return useQuery({
    queryKey: queryKeys.households,
    queryFn: () => apiGet<HouseholdListItemDto[]>('/households'),
  })
}

/** Active + archived households — for the switcher's "Arkiverade" section (ADR-0016). */
export function useHouseholdsWithArchived() {
  return useQuery({
    queryKey: queryKeys.householdsWithArchived,
    queryFn: () =>
      apiGet<HouseholdListItemDto[]>('/households?includeArchived=true'),
  })
}

/** Debt figures + guard flags for the leave/archive confirmation sheets. */
export function useRemovalPreview(id: string | undefined, enabled = true) {
  return useQuery({
    queryKey: queryKeys.removalPreview(id),
    queryFn: () => apiGet<RemovalPreviewDto>(`/households/${id}/removal-preview`),
    enabled: !!id && enabled,
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

/**
 * Every response is viewer-relative, so login/register/logout invalidate the whole
 * cache — the same rule the old dev user-switcher followed when swapping the acting
 * user. `me` is also set directly (not just invalidated): invalidation only
 * schedules a background refetch, leaving a window where RequireAuth would still
 * see the previous (401) result and bounce straight back to /login before that
 * refetch resolves.
 */
export function useRegister() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: RegisterRequest) => apiPost<MeDto>('/auth/register', body),
    onSuccess: (me) => {
      qc.setQueryData(queryKeys.me, me)
      qc.invalidateQueries()
    },
  })
}

export function useLogin() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: LoginRequest) => apiPost<MeDto>('/auth/login', body),
    onSuccess: (me) => {
      qc.setQueryData(queryKeys.me, me)
      qc.invalidateQueries()
    },
  })
}

export function useLogout() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => apiPost<void>('/auth/logout'),
    onSuccess: () => {
      qc.setQueryData(queryKeys.me, undefined)
      qc.invalidateQueries()
    },
  })
}

/**
 * Update the acting member's own profile (name + avatar emoji, ADR-0019). The response is
 * the fresh MeDto, set directly into the `me` cache so every avatar (header, ledger, …)
 * reflects the change immediately, then broadly invalidated since member avatars ride along
 * on household-scoped reads (summary/entries/recurring) too.
 */
export function useUpdateMe() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateMeRequest) => apiPut<MeDto>('/me', body),
    onSuccess: (me) => {
      qc.setQueryData(queryKeys.me, me)
      qc.invalidateQueries()
    },
  })
}

export function useResendVerification() {
  return useMutation({
    mutationFn: () => apiPost<void>('/auth/resend-verification'),
  })
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: (body: ForgotPasswordRequest) => apiPost<void>('/auth/forgot-password', body),
  })
}

export function useResetPassword() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: ResetPasswordRequest) => apiPost<MeDto>('/auth/reset-password', body),
    onSuccess: (me) => {
      qc.setQueryData(queryKeys.me, me)
      qc.invalidateQueries()
    },
  })
}

export function useSendInvite(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateInviteRequest) =>
      apiPost<InviteDto>(`/households/${householdId}/invites`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: queryKeys.householdInvites(householdId) }),
  })
}

// --- Contacts (ADR-0019) ----------------------------------------------------

/** Accepted contacts — the Member↔Member edges (connection-on-accept). */
export function useContacts() {
  return useQuery({
    queryKey: queryKeys.contacts,
    queryFn: () => apiGet<ContactDto[]>('/contacts'),
  })
}

/** Invites the current user sent that haven't been accepted yet ("Väntar på svar"). */
export function usePendingContactInvites() {
  return useQuery({
    queryKey: queryKeys.pendingContactInvites,
    queryFn: () => apiGet<PendingInviteDto[]>('/contacts/pending'),
  })
}

/** Saved contacts with their status for one household (member | pending | invitable). */
export function useInvitableContacts(householdId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.invitableContacts(householdId),
    queryFn: () => apiGet<InvitableContactDto[]>(`/households/${householdId}/invitable-contacts`),
    enabled: !!householdId,
  })
}

/**
 * Send a blind invite (SMS or email). Typing a number reveals nothing about whether it's on
 * Settl — the response is always the same "sent" shape (ADR-0019). Refreshes the pending list
 * and, when a household was attached, that household's invitable-contacts view.
 */
export function useSendContactInvite() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateContactInviteRequest) =>
      apiPost<ContactInviteResultDto>('/contacts/invites', body),
    onSuccess: (_data, body) => {
      qc.invalidateQueries({ queryKey: queryKeys.pendingContactInvites })
      if (body.householdId)
        qc.invalidateQueries({ queryKey: queryKeys.invitableContacts(body.householdId) })
    },
  })
}

/** Invite a saved contact (by member id) to a household — the "reuse a saved contact" flow. */
export function useInviteContactToHousehold(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: InviteContactRequest) =>
      apiPost<ContactInviteResultDto>(`/households/${householdId}/invite-contact`, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.invitableContacts(householdId) })
      qc.invalidateQueries({ queryKey: queryKeys.householdInvites(householdId) })
    },
  })
}

/** Update the acting member's own profile (the optional, unverified phone). */
export function useUpdateProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateProfileRequest) => apiPatch<MeDto>('/me', body),
    onSuccess: (me) => qc.setQueryData(queryKeys.me, me),
  })
}

export function useAcceptInvite(token: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: AcceptInviteRequest) =>
      apiPost<MeDto>(`/invites/${token}/accept`, body),
    onSuccess: (me) => {
      // Same reasoning as useLogin: set directly so a same-tick navigate to '/'
      // doesn't see stale (unauthenticated) cache before the invalidation refetch.
      qc.setQueryData(queryKeys.me, me)
      qc.invalidateQueries()
    },
  })
}

export function useCreateHousehold() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateHouseholdRequest) => apiPost<HouseholdDto>('/households', body),
    onSuccess: () => invalidateHouseholdLists(qc),
  })
}

/** Both household lists (active + archived) plus a household's members/detail — the
 * surfaces every ownership/archival mutation can move. Returns the invalidation
 * promise so callers (via mutation `onSuccess`) await the refetch before acting on
 * the new list — e.g. the create flow switches into the fresh household only once
 * it's present in the cache, avoiding the active-household reset race. */
function invalidateHouseholdMembership(qc: QueryClient, householdId: string | undefined) {
  const lists = invalidateHouseholdLists(qc)
  if (!householdId) return lists
  return Promise.all([
    lists,
    qc.invalidateQueries({ queryKey: queryKeys.household(householdId) }),
    qc.invalidateQueries({ queryKey: queryKeys.members(householdId) }),
    qc.invalidateQueries({ queryKey: queryKeys.removalPreview(householdId) }),
  ])
}

function invalidateHouseholdLists(qc: QueryClient) {
  return Promise.all([
    qc.invalidateQueries({ queryKey: queryKeys.households }),
    qc.invalidateQueries({ queryKey: queryKeys.householdsWithArchived }),
  ])
}

/** Owner hands ownership to another current member (ADR-0016). */
export function useTransferOwnership(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: TransferOwnershipRequest) =>
      apiPost<HouseholdDto>(`/households/${householdId}/transfer-ownership`, body),
    onSuccess: () => invalidateHouseholdMembership(qc, householdId),
  })
}

/** Leave a household. Sole-owner leaving archives it instead (LeaveResultDto.archived). */
export function useLeaveHousehold(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => apiPost<LeaveResultDto>(`/households/${householdId}/leave`),
    onSuccess: () => invalidateHouseholdMembership(qc, householdId),
  })
}

/** Soft-archive the whole household (owner-only). */
export function useArchiveHousehold(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => apiPost<HouseholdDto>(`/households/${householdId}/archive`),
    onSuccess: () => invalidateHouseholdMembership(qc, householdId),
  })
}

/** Restore an archived household (owner-only). */
export function useRestoreHousehold(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => apiPost<HouseholdDto>(`/households/${householdId}/restore`),
    onSuccess: () => invalidateHouseholdMembership(qc, householdId),
  })
}

/** Permanently delete an empty household (owner-only, no ledger activity). ADR-0022. */
export function useDeleteHousehold(householdId: string | undefined) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => apiDelete<void>(`/households/${householdId}`),
    onSuccess: () => invalidateHouseholdMembership(qc, householdId),
  })
}

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

/**
 * Hard-delete a recurring template (ADR-0018 delete-if-clean). The API 409s when the
 * template has posted history; the caller surfaces that and falls back to pausing.
 */
export function useDeleteRecurring(householdId?: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (recId: string) => apiDelete<void>(`/recurring/${recId}`),
    onSuccess: (_data, recId) => {
      qc.removeQueries({ queryKey: queryKeys.recurringDetail(recId) })
      invalidateHousehold(qc, householdId)
    },
  })
}
