/**
 * Tiny persisted external stores (no deps). Backed by localStorage; usable both
 * outside React (the api.ts header) and inside via `useSyncExternalStore`.
 * These hold client-only selection state:
 *   - currentMemberId → the dev "acting user" (X-Settl-User header, tech-debt 0003)
 *   - activeHouseholdId → the active household (user↔household is many-to-many)
 */

type Listener = () => void

export interface PersistedStore {
  get: () => string | null
  set: (value: string | null) => void
  subscribe: (listener: Listener) => () => void
}

function createPersistedStore(key: string): PersistedStore {
  let value: string | null = null
  try {
    value = localStorage.getItem(key)
  } catch {
    value = null
  }
  const listeners = new Set<Listener>()

  return {
    get: () => value,
    set: (next) => {
      if (next === value) return
      value = next
      try {
        if (next === null) localStorage.removeItem(key)
        else localStorage.setItem(key, next)
      } catch {
        /* ignore storage failures (private mode etc.) */
      }
      listeners.forEach((l) => l())
    },
    subscribe: (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
  }
}

export const currentMemberIdStore = createPersistedStore('settl.currentMemberId')
export const activeHouseholdIdStore = createPersistedStore('settl.activeHouseholdId')
