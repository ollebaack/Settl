/**
 * Hushåll `/household` — household management screen (ADR-0016 /
 * docs/specs/household-ownership.md, design frames 1 & 2). Shows the active household,
 * its members with an "Ägare" pill on the owner, and owner-vs-member actions (transfer +
 * archive for the owner; leave for members). Ownership/archival are DERIVED and enforced
 * server-side — this screen renders and calls (ADR-0006).
 */
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { ChevronLeftIcon } from 'lucide-react'

import { MemberAvatar } from '@/components/member-avatar'
import { RequireAuth } from '@/components/require-auth'
import { ErrorState, LoadingState, NoHouseholdState } from '@/components/screen-states'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { cn } from '@/lib/utils'
import { useActiveHousehold } from '@/lib/active-household'
import { useHousehold, useMe } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'

export const Route = createFileRoute('/household')({
  component: () => (
    <RequireAuth>
      <HouseholdManagePage />
    </RequireAuth>
  ),
})

const SECTION_LABEL = 'text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground'
const DANGER_GHOST = 'w-full border-destructive text-destructive hover:bg-destructive/10'

function HouseholdManagePage() {
  const navigate = useNavigate()
  const { householdId, isLoading: householdsLoading } = useActiveHousehold()
  const { openSheet } = useSheet()
  const household = useHousehold(householdId)
  const me = useMe()

  if (householdsLoading || household.isLoading) return <LoadingState rows={4} />
  if (!householdId) return <NoHouseholdState onCreate={() => openSheet('newHousehold')} />
  if (household.isError) return <ErrorState error={household.error} onRetry={() => household.refetch()} />
  if (!household.data) return null

  const hh = household.data
  const ownerName = hh.members.find((m) => m.id === hh.ownerMemberId)?.name ?? 'ägaren'

  return (
    <div className="flex flex-col">
      <div className="mb-4 flex items-center justify-between">
        <Button
          variant="ghost"
          size="sm"
          className="-ml-2 gap-1 text-muted-foreground"
          onClick={() => navigate({ to: '/', search: {} })}
        >
          <ChevronLeftIcon className="size-4" />
          Tillbaka
        </Button>
        <span className={SECTION_LABEL}>Hushåll</span>
        <span className="w-14" />
      </div>

      <div className="flex items-center gap-3.5">
        <span
          aria-hidden="true"
          className="grid size-[46px] shrink-0 place-items-center rounded-2xl bg-accent text-lg font-bold text-accent-foreground"
        >
          {(hh.name.trim()[0] ?? '?').toUpperCase()}
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-lg font-bold tracking-tight">{hh.name}</p>
          <p className="text-[12.5px] text-muted-foreground">
            {hh.members.length} {hh.members.length === 1 ? 'medlem' : 'medlemmar'} · {hh.currency}
          </p>
        </div>
      </div>

      <p className={cn(SECTION_LABEL, 'mt-6 mb-2')}>Medlemmar</p>
      <Card size="sm" className="gap-0 px-4 py-0">
        {hh.members.map((m, i) => {
          const isSelf = m.id === me.data?.id
          const isOwnerRow = m.id === hh.ownerMemberId
          return (
            <div
              key={m.id}
              className={cn(
                'flex items-center gap-3 py-2.5',
                i < hh.members.length - 1 && 'border-b border-border',
              )}
            >
              <MemberAvatar name={m.name} avatarColor={m.avatarColor} size="sm" />
              <span className="flex-1 text-sm font-semibold">{isSelf ? 'Du' : m.name}</span>
              {isOwnerRow && (
                <Badge className="bg-accent uppercase tracking-wide text-accent-foreground">
                  Ägare
                </Badge>
              )}
            </div>
          )
        })}
      </Card>

      <div className="mt-5 flex flex-col gap-2.5">
        {hh.isOwner ? (
          <>
            <Button
              variant="outline"
              className="w-full"
              onClick={() => openSheet('transferOwnership', { id: hh.id })}
            >
              Överför ägarskap
            </Button>
            <Button
              variant="outline"
              className={DANGER_GHOST}
              onClick={() => openSheet('archiveHousehold', { id: hh.id })}
            >
              Arkivera hushåll
            </Button>
          </>
        ) : (
          <Button
            variant="outline"
            className={DANGER_GHOST}
            onClick={() => openSheet('leaveHousehold', { id: hh.id })}
          >
            Lämna hushåll
          </Button>
        )}
      </div>

      <p className="mt-3 text-center text-[13px] text-muted-foreground">
        {hh.isOwner
          ? 'Du äger hushållet. För att lämna — överför ägarskapet först.'
          : `Bara ägaren (${ownerName}) kan arkivera hushållet.`}
      </p>
    </div>
  )
}
