/**
 * STUB — Dina hushåll (switch household; many-to-many). Fleshed out in Phase 5B.
 * Implementation-map §2.5.
 */
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { SheetTodo } from '@/components/sheets/sheet-todo'

export function HouseholdSwitcherSheet({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Dina hushåll"
      description="En bok per hushåll — du kan vara med i flera."
    >
      <SheetTodo screen="Dina hushåll" />
    </ResponsiveSheet>
  )
}
