/**
 * STUB — Ny post (create expense / IOU / recurring). Fleshed out in Phase 5B.
 * Implementation-map §2.6. Stable import path for SheetRouter.
 */
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { SheetTodo } from '@/components/sheets/sheet-todo'

export function AddEntrySheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Ny post"
    >
      <SheetTodo screen="Ny post" />
    </ResponsiveSheet>
  )
}
