import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/activity')({
  component: ActivityPage,
})

function ActivityPage() {
  // filled in Phase 5B — Knuffar: nudge cards (3 triggers), empty state
  return (
    <div className="space-y-4">
      <h1 className="font-heading text-lg font-bold tracking-tight">Knuffar</h1>
      <p className="text-sm text-muted-foreground">Settl säger bara till när något händer.</p>
    </div>
  )
}
