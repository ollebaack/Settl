/**
 * Client-side deferred delete with an "Ångra" (undo) window (ledger-editing spec §2.3 +
 * tech-debt 0010). Confirming a delete optimistically drops the entry from the
 * ledger list and shows a ~5 s Sonner toast; the real DELETE fires only after the
 * window elapses. Ångra cancels the pending call so nothing ever hit the server.
 *
 * Deliberately best-effort and per-device: the timer is plain in-memory, so closing
 * or reloading the tab abandons the pending delete and the entry resurrects on the
 * next load. Undo is delete-only — a mis-edit is recovered by editing again.
 */
import { useCallback } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'

import { apiDelete, type EntryDto } from './api'
import { invalidateHousehold, queryKeys } from './queries'

export const DELETE_UNDO_MS = 5000

export function useDeferredDeleteEntry(householdId?: string) {
  const qc = useQueryClient()

  return useCallback(
    (entry: Pick<EntryDto, 'id'>) => {
      // Optimistically remove from every cached entries list for this household.
      qc.setQueriesData<EntryDto[]>({ queryKey: queryKeys.entriesAll(householdId) }, (old) =>
        Array.isArray(old) ? old.filter((e) => e.id !== entry.id) : old,
      )
      qc.removeQueries({ queryKey: queryKeys.entry(entry.id) })

      let resolved = false
      const commit = async () => {
        if (resolved) return
        resolved = true
        try {
          await apiDelete<void>(`/entries/${entry.id}`)
          invalidateHousehold(qc, householdId)
        } catch (e) {
          toast.error(e instanceof Error ? e.message : 'Kunde inte ta bort posten')
          invalidateHousehold(qc, householdId) // resurrect on failure
        }
      }

      const timer = setTimeout(commit, DELETE_UNDO_MS)

      toast('Posten togs bort', {
        duration: DELETE_UNDO_MS,
        action: {
          label: 'Ångra',
          onClick: () => {
            if (resolved) return
            resolved = true
            clearTimeout(timer)
            invalidateHousehold(qc, householdId) // restore the list
          },
        },
      })
    },
    [qc, householdId],
  )
}
