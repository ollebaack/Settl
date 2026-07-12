/** Reactive media-query hook via useSyncExternalStore (no effect-based fetching). */
import { useSyncExternalStore } from 'react'

/** The responsive breakpoint for the desktop shell (implementation-map §3). */
export const WIDE_QUERY = '(min-width: 980px)'

export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const mql = window.matchMedia(query)
      mql.addEventListener('change', onChange)
      return () => mql.removeEventListener('change', onChange)
    },
    () => window.matchMedia(query).matches,
    () => false,
  )
}

/** `true` on the desktop shell (innerWidth ≥ 980). */
export function useIsWide(): boolean {
  return useMediaQuery(WIDE_QUERY)
}
