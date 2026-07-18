/**
 * Shared open-state for the iOS install guide (installable-pwa spec). Lives outside React
 * because the sheet is opened both by its own one-time auto-prompt and from the
 * account menu; kept in its own module so the component file exports only a
 * component (fast-refresh friendly).
 */
import { useSyncExternalStore } from 'react'

let isOpen = false
const listeners = new Set<() => void>()

export function setInstallSheetOpen(next: boolean) {
  if (next === isOpen) return
  isOpen = next
  listeners.forEach((l) => l())
}

/** Open the install guide from anywhere (e.g. the account menu). */
export function openInstallSheet() {
  setInstallSheetOpen(true)
}

export function useInstallSheetOpen(): boolean {
  return useSyncExternalStore(
    (cb) => {
      listeners.add(cb)
      return () => listeners.delete(cb)
    },
    () => isOpen,
    () => false,
  )
}
