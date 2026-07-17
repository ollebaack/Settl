/**
 * Overlay (bottom-sheet / dialog) routing via URL search params, so overlays are
 * deep-linkable and the underlying screen stays mounted. Only one open at a time.
 * The search schema is validated on the root route (see routes/__root.tsx).
 */
import { useCallback } from 'react'
import { useNavigate, useSearch } from '@tanstack/react-router'

export type SheetKind =
  | 'add'
  | 'edit'
  | 'editRecurring'
  | 'entry'
  | 'settle'
  | 'households'
  | 'newHousehold'
  | 'recurring'
  | 'addFriend'
  | 'leaveHousehold'
  | 'transferOwnership'
  | 'archiveHousehold'
  | 'deleteHousehold'

export interface SheetSearch {
  sheet?: SheetKind
  /** entry id (entry / edit sheet) or recurring id (recurring / editRecurring sheet). */
  id?: string
  /** member id (settle sheet). */
  person?: string
}

const SHEET_KINDS: readonly SheetKind[] = [
  'add',
  'edit',
  'editRecurring',
  'entry',
  'settle',
  'households',
  'newHousehold',
  'recurring',
  'addFriend',
  'leaveHousehold',
  'transferOwnership',
  'archiveHousehold',
  'deleteHousehold',
]

/** Validate/normalise the sheet search params (used by the root route). */
export function validateSheetSearch(search: Record<string, unknown>): SheetSearch {
  const rawSheet = search.sheet
  const sheet =
    typeof rawSheet === 'string' && (SHEET_KINDS as readonly string[]).includes(rawSheet)
      ? (rawSheet as SheetKind)
      : undefined
  return {
    sheet,
    id: typeof search.id === 'string' ? search.id : undefined,
    person: typeof search.person === 'string' ? search.person : undefined,
  }
}

export interface UseSheetResult extends SheetSearch {
  openSheet: (kind: SheetKind, params?: { id?: string; person?: string }) => void
  closeSheet: () => void
}

/**
 * Read the active sheet and get imperative open/close helpers. All three
 * (state + `openSheet` + `closeSheet`) are returned from this one hook because
 * navigation requires router context.
 */
export function useSheet(): UseSheetResult {
  const navigate = useNavigate()
  const search = useSearch({ strict: false }) as SheetSearch

  const openSheet = useCallback<UseSheetResult['openSheet']>(
    (kind, params) => {
      navigate({
        to: '.',
        search: (prev: Record<string, unknown>) => ({
          ...prev,
          sheet: kind,
          id: params?.id,
          person: params?.person,
        }),
      })
    },
    [navigate],
  )

  const closeSheet = useCallback<UseSheetResult['closeSheet']>(() => {
    navigate({
      to: '.',
      search: (prev: Record<string, unknown>) => ({
        ...prev,
        sheet: undefined,
        id: undefined,
        person: undefined,
      }),
    })
  }, [navigate])

  return {
    sheet: search.sheet,
    id: search.id,
    person: search.person,
    openSheet,
    closeSheet,
  }
}
