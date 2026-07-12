/**
 * STUB — Recurring detail (next post, split, past cycles, pause/resume).
 * Fleshed out in Phase 5B. Implementation-map §2.9.
 */
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { SheetTodo } from '@/components/sheets/sheet-todo'

export function RecurringDetailSheet({
  open,
  onClose,
  recId,
}: {
  open: boolean
  onClose: () => void
  recId?: string
}) {
  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="På repeat"
    >
      <SheetTodo screen="Återkommande" targetId={recId} />
    </ResponsiveSheet>
  )
}
