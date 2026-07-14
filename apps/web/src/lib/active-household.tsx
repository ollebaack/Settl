/**
 * Active-household store. User↔household is many-to-many, so the app always
 * tracks which household's book is in view. Defaults to the first household
 * from GET /households; persisted in localStorage.
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react'
import { activeHouseholdIdStore } from './stores'
import { useHouseholds } from './queries'
import type { HouseholdListItemDto } from './api'

interface ActiveHouseholdContextValue {
  householdId: string | undefined
  household: HouseholdListItemDto | undefined
  households: HouseholdListItemDto[]
  /** True until GET /households has resolved at least once. */
  isLoading: boolean
  setHouseholdId: (id: string) => void
}

const ActiveHouseholdContext = createContext<ActiveHouseholdContextValue | null>(null)

export function ActiveHouseholdProvider({ children }: { children: ReactNode }) {
  const stored = useSyncExternalStore(activeHouseholdIdStore.subscribe, activeHouseholdIdStore.get)
  const { data: households, isPending } = useHouseholds()

  // Default to (or fall back to) the first household the user belongs to.
  useEffect(() => {
    if (!households || households.length === 0) return
    const known = households.some((h) => h.id === stored)
    if (!stored || !known) {
      activeHouseholdIdStore.set(households[0].id)
    }
  }, [stored, households])

  const setHouseholdId = useCallback((id: string) => {
    activeHouseholdIdStore.set(id)
  }, [])

  const householdId = stored ?? households?.[0]?.id
  const value = useMemo<ActiveHouseholdContextValue>(
    () => ({
      householdId,
      household: households?.find((h) => h.id === householdId),
      households: households ?? [],
      isLoading: isPending,
      setHouseholdId,
    }),
    [householdId, households, isPending, setHouseholdId],
  )

  return (
    <ActiveHouseholdContext.Provider value={value}>
      {children}
    </ActiveHouseholdContext.Provider>
  )
}

export function useActiveHousehold(): ActiveHouseholdContextValue {
  const ctx = useContext(ActiveHouseholdContext)
  if (!ctx) throw new Error('useActiveHousehold must be used within an ActiveHouseholdProvider')
  return ctx
}
