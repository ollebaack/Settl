/**
 * STUB — Entry detail (inspect entry, per-person shares, settle/reopen).
 * Fleshed out in Phase 5B. Implementation-map §2.7.
 */
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { SheetTodo } from '@/components/sheets/sheet-todo'

export function EntryDetailSheet({
  open,
  onClose,
  entryId,
}: {
  open: boolean
  onClose: () => void
  entryId?: string
}) {
  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Postdetalj"
    >
      <SheetTodo screen="Postdetalj" targetId={entryId} />
    </ResponsiveSheet>
  )
}
