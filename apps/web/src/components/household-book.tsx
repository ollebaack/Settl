/**
 * HouseholdBook — the merged "Hushållet" page (ADR-0020 +
 * docs/design/home-hushallet-addendum.md §2.2). One scroll for a single book:
 * net balance hero → per-person balances (each with a prominent "Gör upp"
 * action) → mobile-only "På gång" upcoming rail → the full filterable Loggbok
 * grouped by day. Replaces the old split of Home dashboard + a separate Loggbok
 * tab.
 *
 * It is the "in a book" view, rendered at `/hushallet` for the active household
 * (the Hushållet tab) and at `/hushall/$id` for a book drilled into from the
 * overview. All server state comes from the shared TanStack Query hooks, scoped
 * to one household; the screen renders and calls, never computes (ADR-0006).
 */
import { useState } from 'react'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { Money, type MoneyIntent } from '@/components/money'
import { MemberAvatar } from '@/components/member-avatar'
import { GhostCard } from '@/components/ghost-card'
import { EntryRow } from '@/components/entry-row'
import { EmptyState, ErrorState, LoadingState } from '@/components/screen-states'
import { useEntries, useMembers, useSummary } from '@/lib/queries'
import { useActiveHousehold } from '@/lib/active-household'
import { useSheet } from '@/lib/sheet'
import { useIsWide } from '@/lib/use-media'
import { dayGroupLabel, inDays, shortDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { EntryDto, EntryFilter, MemberDto, PersonBalanceDto, UpcomingDto } from '@/lib/api'

/** Coerce an int64-as-`number | string` (openapi) field to a number. */
function toNum(value: number | string): number {
  return typeof value === 'number' ? value : Number(value)
}

const SECTION_LABEL =
  'text-xs font-semibold uppercase tracking-[0.09em] text-muted-foreground'

interface FilterOption {
  value: EntryFilter
  label: string
}

const FILTERS: FilterOption[] = [
  { value: 'all', label: 'Alla' },
  { value: 'expense', label: 'Utgifter' },
  { value: 'recurring', label: 'Repeat' },
]

/** Empty-state copy per filter (implementation-map §2.2 / ambiguity #11). */
const EMPTY_COPY: Record<EntryFilter, string> = {
  all: 'Inga poster än',
  expense: 'Inga utgifter än',
  recurring: 'Inga repeat-poster än',
}

interface DayGroup {
  key: string
  label: string
  entries: EntryDto[]
}

/** Group already-date-desc-sorted entries into ordered per-day buckets. */
function groupByDay(entries: EntryDto[]): DayGroup[] {
  const groups: DayGroup[] = []
  let current: DayGroup | undefined
  for (const entry of entries) {
    if (!current || current.key !== entry.date) {
      current = { key: entry.date, label: dayGroupLabel(entry.date), entries: [] }
      groups.push(current)
    }
    current.entries.push(entry)
  }
  return groups
}

export function HouseholdBook({ householdId }: { householdId: string }) {
  const { households } = useActiveHousehold()
  const household = households.find((h) => h.id === householdId)
  const wide = useIsWide()
  const { openSheet } = useSheet()
  const [filter, setFilter] = useState<EntryFilter>('all')

  const summary = useSummary(householdId)
  const members = useMembers(householdId)
  const entriesQuery = useEntries(householdId, { type: filter })

  if (summary.isPending) {
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

      {/* Per-person balances — each with a prominent "Gör upp" action */}
      {data.people.length > 0 && (
        <section>
          <p className={SECTION_LABEL}>Saldon</p>
          <div className="mt-2 flex flex-col gap-2">
            {data.people.map((person) => (
              <PersonRow
                key={person.memberId}
                person={person}
                onSettle={() => openSheet('settle', { person: person.memberId })}
              />
            ))}
          </div>
        </section>
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

      {/* Loggbok — filterable, day-grouped log (folded in, ADR-0020) */}
      <section>
        <div className="flex items-baseline justify-between">
          <p className={SECTION_LABEL}>Loggbok</p>
          {!entriesQuery.isPending && (entriesQuery.data?.length ?? 0) > 0 && (
            <span className="text-[11.5px] text-muted-foreground">
              {entriesQuery.data?.length} poster
            </span>
          )}
        </div>

        <ToggleGroup
          aria-label="Filtrera loggboken"
          value={[filter]}
          onValueChange={(next) => setFilter((next[0] as EntryFilter | undefined) ?? filter)}
          className="mt-2.5 flex-wrap"
        >
          {FILTERS.map((f) => (
            <ToggleGroupItem
              key={f.value}
              value={f.value}
              className={cn(
                'h-8 rounded-full bg-muted px-3.5 text-xs text-muted-foreground',
                'hover:bg-muted hover:text-foreground',
                'aria-pressed:bg-foreground aria-pressed:text-background',
                'data-[pressed]:bg-foreground data-[pressed]:text-background',
              )}
            >
              {f.label}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>

        <LedgerBody
          filter={filter}
          isLoading={entriesQuery.isLoading}
          isError={entriesQuery.isError}
          error={entriesQuery.error}
          groups={groupByDay(entriesQuery.data ?? [])}
          members={members.data}
          onRetry={() => entriesQuery.refetch()}
          onOpenEntry={(id) => openSheet('entry', { id })}
        />
      </section>
    </div>
  )
}

// --- Per-person balance row -------------------------------------------------

function PersonRow({
  person,
  onSettle,
}: {
  person: PersonBalanceDto
  onSettle: () => void
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
      data-testid="person-row"
      onClick={onSettle}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSettle()
        }
      }}
      className="flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring"
    >
      <MemberAvatar name={person.name} avatarColor={person.avatarColor} avatarEmoji={person.avatarEmoji} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{person.name}</p>
        {isSquare ? (
          <p className="truncate text-xs text-muted-foreground">allt är kvitt</p>
        ) : (
          <p className="truncate text-xs">
            <Money minor={person.netMinor} intent={intent} className="text-xs" />
            <span className="text-muted-foreground"> · {relation}</span>
          </p>
        )}
      </div>
      {isSquare ? (
        <span className="shrink-0 rounded-full bg-muted px-2.5 py-1 text-[11px] font-semibold text-muted-foreground">
          Kvitt
        </span>
      ) : (
        <Button
          size="sm"
          className="shrink-0 rounded-full"
          onClick={(e) => {
            e.stopPropagation()
            onSettle()
          }}
        >
          Gör upp
        </Button>
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

// --- Loggbok body (loading / error / empty-per-filter / day groups) ---------

function LedgerBody({
  filter,
  isLoading,
  isError,
  error,
  groups,
  members,
  onRetry,
  onOpenEntry,
}: {
  filter: EntryFilter
  isLoading: boolean
  isError: boolean
  error: unknown
  groups: DayGroup[]
  members: MemberDto[] | undefined
  onRetry: () => void
  onOpenEntry: (id: string) => void
}) {
  if (isLoading) {
    return (
      <div className="mt-4 space-y-5">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="space-y-2">
            <div className="h-3 w-16 rounded bg-muted" />
            <LoadingState rows={2} />
          </div>
        ))}
      </div>
    )
  }

  if (isError) {
    return <ErrorState error={error} onRetry={onRetry} className="mt-4" />
  }

  if (groups.length === 0) {
    return <EmptyState className="mt-4">{EMPTY_COPY[filter]}</EmptyState>
  }

  return (
    <div className="mt-4 space-y-5">
      {groups.map((group) => (
        <section key={group.key} className="space-y-2">
          <h2 className="px-1 text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground">
            {group.label}
          </h2>
          <div className="space-y-2">
            {group.entries.map((entry) => (
              <EntryRow key={entry.id} entry={entry} members={members} onOpen={onOpenEntry} />
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
