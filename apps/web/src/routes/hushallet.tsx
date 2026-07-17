/**
 * `/hushallet` — the Hushållet tab (ADR-0020): the merged book page for the
 * ACTIVE household, with a back-to-overview affordance. Picking a book on the
 * overview sets it active and lands here; the same <HouseholdBook> also backs
 * the `/hushall/$id` drill-in. If the user has no household, fall back to `/`
 * (the overview handles the first-run create flow).
 */
import { Link, Navigate, createFileRoute } from '@tanstack/react-router'
import { ArrowLeftIcon } from 'lucide-react'
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

  return (
    <div className="flex flex-col gap-4">
      <Link
        to="/"
        search={{}}
        className="inline-flex w-fit items-center gap-1.5 rounded text-[13px] font-semibold text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
      >
        <ArrowLeftIcon className="size-4" />
        Översikt
      </Link>
      <HouseholdBook householdId={householdId} />
    </div>
  )
}
