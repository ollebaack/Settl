import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/recurring')({
  component: RecurringPage,
})

function RecurringPage() {
  // filled in Phase 5B — På repeat: summary tiles, template cards, cycle progress
  return (
    <div className="space-y-4">
      <h1 className="font-heading text-lg font-bold tracking-tight">På repeat</h1>
      <p className="text-sm text-muted-foreground">Återkommande kostnader byggs i Phase 5B.</p>
    </div>
  )
}
