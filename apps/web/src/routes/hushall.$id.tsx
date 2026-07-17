/**
 * `/hushall/$id` — a single book entered by id (e.g. a deep link, or the
 * overview drill-in). `/` is the overview (ADR-0020), so a focused book needs a
 * home distinct from it; this reuses the shared <HouseholdBook> (the same merged
 * page as the Hushållet tab) and adds a back-to-overview affordance. Entering a
 * book here makes it the active household so the rest of the app chrome
 * (switcher, FAB, right rail, sheets) follows it as usual.
 */
import { useEffect } from 'react'
import { Link, Navigate, createFileRoute } from '@tanstack/react-router'
import { ArrowLeftIcon } from 'lucide-react'
import { RequireAuth } from '@/components/require-auth'
import { HouseholdBook } from '@/components/household-book'
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
      <HouseholdBook householdId={id} />
    </div>
  )
}
