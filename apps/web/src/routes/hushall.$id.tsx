/**
 * `/hushall/$id` — a single book's focused dashboard (ADR-0019 §5.1). For a
 * multi-household user `/` is the overview, so drilling into one book needs a
 * home distinct from it; this reuses the shared HouseholdDashboard and adds a
 * back-to-overview affordance. Entering a book here makes it the active
 * household so the rest of the app chrome (switcher, FAB, right rail, sheets)
 * follows it as usual.
 */
import { useEffect } from 'react'
import { Link, Navigate, createFileRoute } from '@tanstack/react-router'
import { ArrowLeftIcon } from 'lucide-react'
import { RequireAuth } from '@/components/require-auth'
import { HouseholdDashboard } from '@/components/household-dashboard'
import { LoadingState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'

export const Route = createFileRoute('/hushall/$id')({
  component: () => (
    <RequireAuth>
      <HouseholdRoute />
    </RequireAuth>
  ),
})

function HouseholdRoute() {
  const { id } = Route.useParams()
  const { households, isLoading, householdId, setHouseholdId } = useActiveHousehold()

  const belongs = households.some((h) => h.id === id)

  // Enter this book: make it the active household so the shell chrome + sheets
  // target it. Only after we've confirmed the user belongs to it.
  useEffect(() => {
    if (belongs && householdId !== id) {
      setHouseholdId(id)
    }
  }, [belongs, householdId, id, setHouseholdId])

  if (isLoading) {
    return <LoadingState hero rows={3} />
  }

  // Unknown / not-a-member id → fall back to the adaptive home.
  if (!belongs) {
    return <Navigate to="/" search={{}} />
  }

  // The overview only exists at 2+ households; only then is "back to overview"
  // meaningful.
  const showBack = households.length >= 2

  return (
    <div className="flex flex-col gap-4">
      {showBack && (
        <Link
          to="/"
          search={{}}
          className="inline-flex w-fit items-center gap-1.5 rounded text-[13px] font-semibold text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
        >
          <ArrowLeftIcon className="size-4" />
          Översikt
        </Link>
      )}
      <HouseholdDashboard householdId={id} />
    </div>
  )
}
