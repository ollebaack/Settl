import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: HomePage,
})

function HomePage() {
  // filled in Phase 5B — Hem: net hero, per-person balances, upcoming, senaste
  return (
    <div className="space-y-4">
      <h1 className="font-heading text-lg font-bold tracking-tight">Hem</h1>
      <p className="text-sm text-muted-foreground">Startsidan byggs i Phase 5B.</p>
    </div>
  )
}
