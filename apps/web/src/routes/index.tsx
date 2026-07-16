/**
 * Hem `/` — the ADAPTIVE home (ADR-0019). Its shape depends on how many ACTIVE
 * (non-archived) households the user belongs to:
 *   0 → first-run create flow (open the new-household sheet).
 *   1 → that book's dashboard, unchanged (the overview never appears).
 *   2+ → the multi-household overview.
 * The single-household majority pays no tax — the overview simply does not
 * exist for them. All server state comes from the shared TanStack Query hooks
 * (ADR-0006).
 */
import { createFileRoute } from '@tanstack/react-router'
import { useEffect } from 'react'
import { RequireAuth } from '@/components/require-auth'
import { HouseholdDashboard } from '@/components/household-dashboard'
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
  // (ADR-0016) are hidden from this list, so the adaptive threshold naturally
  // counts active books only.
  const activeHouseholds = households
  const hasNoHousehold = !householdsLoading && activeHouseholds.length === 0

  // First-run guidance: a brand-new user has no household yet, so there's
  // nothing for the home screen to show — open the create-household sheet
  // for them instead of leaving them on a screen that loads forever.
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

  // Two or more active books → the cross-household overview.
  if (activeHouseholds.length >= 2) {
    return <Overview households={activeHouseholds} />
  }

  // Exactly one → that book's dashboard, unchanged.
  return <HouseholdDashboard householdId={activeHouseholds[0].id} />
}
