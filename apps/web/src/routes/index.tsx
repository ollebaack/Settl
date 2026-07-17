/**
 * Hem `/` — always the multi-household Overview (ADR-0021). It is the portfolio
 * over the user's active books regardless of how many there are; a single book
 * is entered from here (or via the Hushållet tab). Zero households → first-run
 * create flow. All server state comes from the shared TanStack Query hooks
 * (ADR-0006).
 */
import { createFileRoute } from '@tanstack/react-router'
import { useEffect } from 'react'
import { RequireAuth } from '@/components/require-auth'
import { Overview } from '@/components/overview'
import { LoadingState, NoHouseholdState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'
import { useSheet } from '@/lib/sheet'

export const Route = createFileRoute('/')({
  component: () => (
    <RequireAuth>
      <HomePage />
    </RequireAuth>
  ),
})

function HomePage() {
  const { households, isLoading: householdsLoading } = useActiveHousehold()
  const { sheet, openSheet } = useSheet()

  // GET /households returns the user's ACTIVE books — archived households
  // (ADR-0016) are hidden, so the overview only ever counts active books.
  const activeHouseholds = households
  const hasNoHousehold = !householdsLoading && activeHouseholds.length === 0

  // First-run guidance: a brand-new user has no household yet, so there's
  // nothing for the overview to show — open the create-household sheet for them
  // instead of leaving them on a screen that loads forever.
  useEffect(() => {
    if (hasNoHousehold && !sheet) {
      openSheet('newHousehold')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hasNoHousehold])

  if (householdsLoading) {
    return <LoadingState hero rows={3} />
  }

  if (hasNoHousehold) {
    return <NoHouseholdState onCreate={() => openSheet('newHousehold')} className="mt-6" />
  }

  // One or more active books → the overview (ADR-0021: no adaptive collapse).
  return <Overview households={activeHouseholds} />
}
