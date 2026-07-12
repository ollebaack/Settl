import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/ledger')({
  component: LedgerPage,
})

function LedgerPage() {
  // filled in Phase 5B — Loggboken: filter pills, day-grouped entry rows
  return (
    <div className="space-y-4">
      <h1 className="font-heading text-lg font-bold tracking-tight">Loggboken</h1>
      <p className="text-sm text-muted-foreground">Loggboken byggs i Phase 5B.</p>
    </div>
  )
}
