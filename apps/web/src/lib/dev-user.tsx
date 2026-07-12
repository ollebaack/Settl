/**
 * Dev "acting user" store. Holds the current memberId that backs the
 * `X-Settl-User` header (tech-debt 0003 — real auth is ASP.NET Identity later).
 * Defaults to the first seeded member from GET /dev/users; persisted in
 * localStorage. Switching the user invalidates all cached queries because every
 * response is viewer-relative.
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useSyncExternalStore, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { currentMemberIdStore } from './stores'
import { useDevUsers } from './queries'
import type { MemberDto } from './api'

interface DevUserContextValue {
  memberId: string | undefined
  member: MemberDto | undefined
  users: MemberDto[]
  setMemberId: (id: string) => void
}

const DevUserContext = createContext<DevUserContextValue | null>(null)

export function DevUserProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const stored = useSyncExternalStore(currentMemberIdStore.subscribe, currentMemberIdStore.get)
  const { data: users } = useDevUsers()

  // Establish a default acting user once the seed list is known.
  useEffect(() => {
    if (!stored && users && users.length > 0) {
      currentMemberIdStore.set(users[0].id)
    }
  }, [stored, users])

  const setMemberId = useCallback(
    (id: string) => {
      currentMemberIdStore.set(id)
      // Every cached response is relative to the acting user — refetch all.
      qc.invalidateQueries()
    },
    [qc],
  )

  const memberId = stored ?? users?.[0]?.id
  const value = useMemo<DevUserContextValue>(
    () => ({
      memberId,
      member: users?.find((u) => u.id === memberId),
      users: users ?? [],
      setMemberId,
    }),
    [memberId, users, setMemberId],
  )

  return <DevUserContext.Provider value={value}>{children}</DevUserContext.Provider>
}

export function useDevUser(): DevUserContextValue {
  const ctx = useContext(DevUserContext)
  if (!ctx) throw new Error('useDevUser must be used within a DevUserProvider')
  return ctx
}
