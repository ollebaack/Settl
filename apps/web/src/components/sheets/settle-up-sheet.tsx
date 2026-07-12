/**
 * STUB — Gör upp med {namn} (net with one person, settle all).
 * Fleshed out in Phase 5B. Implementation-map §2.8.
 */
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { SheetTodo } from '@/components/sheets/sheet-todo'

export function SettleUpSheet({
  open,
  onClose,
  person,
}: {
  open: boolean
  onClose: () => void
  person?: string
}) {
  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Gör upp"
    >
      <SheetTodo screen="Gör upp" targetId={person} />
    </ResponsiveSheet>
  )
}
