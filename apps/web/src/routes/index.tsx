/**
 * Hem `/` — Home screen (implementation-map §2.1). Net balance hero, per-person
 * balance rows (→ Settle-up sheet), a mobile-only "På gång" upcoming rail of
 * ghost cards (→ Recurring detail), and a "Senaste" recent list (→ /ledger).
 * All server state comes from the shared TanStack Query hooks; the screen renders
 * and calls, never computes (ADR-0006).
 */
import { Link, createFileRoute } from '@tanstack/react-router'
import { Card } from '@/components/ui/card'
import { Money, type MoneyIntent } from '@/components/money'
import { MemberAvatar } from '@/components/member-avatar'
import { GhostCard } from '@/components/ghost-card'
import { EntryRow } from '@/components/entry-row'
import { EmptyState, ErrorState, LoadingState } from '@/components/screen-states'
import { useEntries, useMembers, useSummary } from '@/lib/queries'
import { useActiveHousehold } from '@/lib/active-household'
import { useSheet } from '@/lib/sheet'
import { useIsWide } from '@/lib/use-media'
import { inDays, shortDate } from '@/lib/format'
import type { PersonBalanceDto, UpcomingDto } from '@/lib/api'

export const Route = createFileRoute('/')({
  component: HomePage,
})

/** Coerce an int64-as-`number | string` (openapi) field to a number. */
function toNum(value: number | string): number {
  return typeof value === 'number' ? value : Number(value)
}

const SECTION_LABEL =
  'text-xs font-semibold uppercase tracking-[0.09em] text-muted-foreground'

function HomePage() {
  const { householdId, household } = useActiveHousehold()
  const wide = useIsWide()
  const { openSheet } = useSheet()

  const summary = useSummary(householdId)
  const members = useMembers(householdId)
  const recent = useEntries(householdId, { limit: 4 })

  // Guard while the active household id is being established / summary loads.
  if (!householdId || summary.isPending) {
    return <LoadingState hero rows={3} />
  }

  if (summary.isError) {
    return <ErrorState error={summary.error} onRetry={() => summary.refetch()} />
  }

  const data = summary.data
  const openCount = toNum(data.openCount)

  // Copy + colour intent come from the server-derived netLabel (ADR-0006),
  // never from the sign of the amount.
  const netLabel =
    data.netLabel === 'owed'
      ? 'Du ska få totalt'
      : data.netLabel === 'owe'
        ? 'Du är skyldig totalt'
        : 'Allt är kvitt'
  const netIntent: MoneyIntent =
    data.netLabel === 'owed' ? 'success' : data.netLabel === 'owe' ? 'destructive' : 'muted'

  const showUpcoming = !wide && data.upcoming.length > 0

  return (
    <div className="flex flex-col gap-5">
      {/* Net hero */}
      <Card className="items-center gap-1.5 px-5 py-6 text-center">
        <p className={SECTION_LABEL}>{netLabel}</p>
        <Money
          minor={data.overallNetMinor}
          intent={netIntent}
          className="text-[38px] leading-none tracking-tight"
        />
        <p className="text-[12.5px] text-muted-foreground">
          {openCount} öppna poster i {household?.name ?? ''}
        </p>
      </Card>

      {/* Per-person balances */}
      {data.people.length > 0 && (
        <div className="flex flex-col gap-2">
          {data.people.map((person) => (
            <PersonRow
              key={person.memberId}
              person={person}
              onOpen={() => openSheet('settle', { person: person.memberId })}
            />
          ))}
        </div>
      )}

      {/* "På gång" upcoming rail — mobile only */}
      {showUpcoming && (
        <section>
          <p className={SECTION_LABEL}>På gång</p>
          <div className="mt-2.5 flex gap-2.5 overflow-x-auto pb-1">
            {data.upcoming.map((item) => (
              <UpcomingCard
                key={item.recurringId}
                item={item}
                onGo={() => openSheet('recurring', { id: item.recurringId })}
              />
            ))}
          </div>
        </section>
      )}

      {/* "Senaste" recent activity */}
      <section>
        <div className="flex items-baseline justify-between">
          <p className={SECTION_LABEL}>Senaste</p>
          <Link
            to="/ledger"
            className="rounded text-xs font-semibold text-primary outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            Visa alla
          </Link>
        </div>
        <RecentList
          recent={recent}
          members={members.data}
          onOpen={(entryId) => openSheet('entry', { id: entryId })}
        />
      </section>
    </div>
  )
}

// --- Per-person balance row -------------------------------------------------

function PersonRow({
  person,
  onOpen,
}: {
  person: PersonBalanceDto
  onOpen: () => void
}) {
  // Relation copy + colour intent are server-derived (ADR-0006).
  const relation =
    person.relation === 'owesYou'
      ? 'är skyldig dig'
      : person.relation === 'youOwe'
        ? 'du är skyldig'
        : 'kvitt'
  const intent: MoneyIntent =
    person.relation === 'owesYou'
      ? 'success'
      : person.relation === 'youOwe'
        ? 'destructive'
        : 'muted'
  const isSquare = person.relation === 'square'

  return (
    <Card
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onOpen()
        }
      }}
      className="flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring"
    >
      <MemberAvatar name={person.name} avatarColor={person.avatarColor} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{person.name}</p>
        <p className="truncate text-xs text-muted-foreground">{relation}</p>
      </div>
      {isSquare ? (
        <span className="font-mono text-sm text-muted-foreground">—</span>
      ) : (
        <Money minor={person.netMinor} intent={intent} className="text-sm" />
      )}
    </Card>
  )
}

// --- Upcoming ghost card ----------------------------------------------------

function UpcomingCard({ item, onGo }: { item: UpcomingDto; onGo: () => void }) {
  return (
    <GhostCard onClick={onGo} className="min-w-[150px] shrink-0 rounded-2xl">
      <span className="block truncate text-[13px] font-semibold">{item.title}</span>
      <span className="mt-0.5 block text-[11.5px] font-semibold text-primary">
        Bokförs {shortDate(item.nextPostDate)} · {inDays(item.nextPostDate)}
      </span>
      <span className="mt-2 block">
        <Money minor={item.yourShareMinor} className="text-[13px]" />
      </span>
      <span className="block text-[10.5px] text-muted-foreground">din del</span>
    </GhostCard>
  )
}

// --- Recent activity list ---------------------------------------------------

function RecentList({
  recent,
  members,
  onOpen,
}: {
  recent: ReturnType<typeof useEntries>
  members: Parameters<typeof EntryRow>[0]['members']
  onOpen: (entryId: string) => void
}) {
  if (recent.isPending) {
    return <LoadingState rows={4} className="mt-2" />
  }
  if (recent.isError) {
    return (
      <ErrorState error={recent.error} onRetry={() => recent.refetch()} className="mt-2" />
    )
  }
  if (recent.data.length === 0) {
    return <EmptyState className="mt-2">Inga poster än</EmptyState>
  }
  return (
    <div className="mt-2 flex flex-col gap-2">
      {recent.data.map((entry) => (
        <EntryRow key={entry.id} entry={entry} members={members} onOpen={onOpen} />
      ))}
    </div>
  )
}
