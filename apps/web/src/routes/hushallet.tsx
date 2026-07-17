/**
 * `/hushallet` — the Hushållet tab (ADR-0021): the merged book page for the
 * ACTIVE household. Back to the overview is the "Hem" tab. Picking a book on the
 * overview sets it active and lands here; the same <HouseholdBook> also backs
 * the `/hushall/$id` drill-in. If the user has no household, fall back to `/`
 * (the overview handles the first-run create flow).
 */
import { Navigate, createFileRoute } from '@tanstack/react-router'
import { RequireAuth } from '@/components/require-auth'
import { HouseholdBook } from '@/components/household-book'
import { LoadingState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'

export const Route = createFileRoute('/hushallet')({
  component: () => (
    <RequireAuth>
      <HushalletRoute />
    </RequireAuth>
  ),
})

function HushalletRoute() {
  const { householdId, isLoading } = useActiveHousehold()

  if (isLoading) {
    return <LoadingState hero rows={3} />
  }

  // No active book to show → the overview (and its create flow) lives at `/`.
  if (!householdId) {
    return <Navigate to="/" search={{}} />
  }

  return <HouseholdBook householdId={householdId} />
}
