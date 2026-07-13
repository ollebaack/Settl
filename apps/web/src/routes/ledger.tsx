/**
 * Loggboken (implementation-map §2.2) — the full chronological ledger.
 * Type-filter pills (Alla / Utgifter / Lån / Repeat) drive useEntries(id,{type});
 * mobile upcoming dashed rows come from useSummary().upcoming and only show when
 * the filter isn't IOU/Expense; entries are grouped client-side by day and
 * rendered as <EntryRow> lists. Loading / empty (per filter) / error states are
 * all handled. Server state comes only from the shared TanStack Query hooks;
 * this screen renders and calls, never computes (ADR-0006).
 */
import { useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { RequireAuth } from '@/components/require-auth'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { EntryRow } from '@/components/entry-row'
import { GhostCard } from '@/components/ghost-card'
import { Money } from '@/components/money'
import { LoadingState, EmptyState, ErrorState } from '@/components/screen-states'
import { cn } from '@/lib/utils'
import { dayGroupLabel, inDays, shortDate } from '@/lib/format'
import { useActiveHousehold } from '@/lib/active-household'
import { useEntries, useMembers, useSummary } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import { useIsWide } from '@/lib/use-media'
import type { EntryDto, EntryFilter, MemberDto, UpcomingDto } from '@/lib/api'

export const Route = createFileRoute('/ledger')({
  component: () => (
    <RequireAuth>
      <LedgerPage />
    </RequireAuth>
  ),
})

interface FilterOption {
  value: EntryFilter
  label: string
}

const FILTERS: FilterOption[] = [
  { value: 'all', label: 'Alla' },
  { value: 'expense', label: 'Utgifter' },
  { value: 'iou', label: 'Lån' },
  { value: 'recurring', label: 'Repeat' },
]

/** Empty-state copy per filter (implementation-map §2.2 / ambiguity #11). */
const EMPTY_COPY: Record<EntryFilter, string> = {
  all: 'Inga poster än',
  expense: 'Inga utgifter än',
  iou: 'Inga lån än',
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

function LedgerPage() {
  const [filter, setFilter] = useState<EntryFilter>('all')
  const { householdId } = useActiveHousehold()
  const { openSheet } = useSheet()
  const isWide = useIsWide()

  const entriesQuery = useEntries(householdId, { type: filter })
  const summaryQuery = useSummary(householdId)
  const { data: members } = useMembers(householdId)

  // Upcoming (mobile only) — hidden for the IOU/Expense filters and on desktop.
  const showUpcoming = !isWide && filter !== 'iou' && filter !== 'expense'
  const upcoming = showUpcoming ? (summaryQuery.data?.upcoming ?? []) : []

  const entries = entriesQuery.data ?? []
  const groups = groupByDay(entries)

  return (
    <div className="space-y-4">
      <h1 className="font-heading text-[19px] font-bold tracking-tight">Loggboken</h1>

      <ToggleGroup
        aria-label="Filtrera loggboken"
        value={[filter]}
        onValueChange={(next) => setFilter((next[0] as EntryFilter | undefined) ?? filter)}
        className="flex-wrap"
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

      {upcoming.length > 0 && (
        <UpcomingRail
          upcoming={upcoming}
          onOpen={(recurringId) => openSheet('recurring', { id: recurringId })}
        />
      )}

      <LedgerBody
        filter={filter}
        isLoading={entriesQuery.isLoading}
        isError={entriesQuery.isError}
        error={entriesQuery.error}
        groups={groups}
        members={members}
        onRetry={() => entriesQuery.refetch()}
        onOpenEntry={(id) => openSheet('entry', { id })}
      />
    </div>
  )
}

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
      <div className="space-y-5">
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
    return <ErrorState error={error} onRetry={onRetry} />
  }

  if (groups.length === 0) {
    return <EmptyState>{EMPTY_COPY[filter]}</EmptyState>
  }

  return (
    <div className="space-y-5">
      {groups.map((group) => (
        <section key={group.key} className="space-y-2">
          <h2 className="px-1 text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground">
            {group.label}
          </h2>
          <div className="space-y-2">
            {group.entries.map((entry) => (
              <EntryRow
                key={entry.id}
                entry={entry}
                members={members}
                onOpen={onOpenEntry}
              />
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}

function UpcomingRail({
  upcoming,
  onOpen,
}: {
  upcoming: UpcomingDto[]
  onOpen: (recurringId: string) => void
}) {
  return (
    <section className="space-y-2">
      <div className="space-y-2">
        {upcoming.map((item) => (
          <GhostCard
            key={item.recurringId}
            onClick={() => onOpen(item.recurringId)}
            className="flex flex-row items-center gap-3"
          >
            <span
              aria-hidden="true"
              className="flex size-8 shrink-0 items-center justify-center rounded-[10px] bg-accent text-sm font-medium text-accent-foreground"
            >
              ↻
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{item.title}</p>
              <p className="truncate text-xs text-accent-foreground">
                Bokförs {shortDate(item.nextPostDate)} · {inDays(item.nextPostDate)}
              </p>
            </div>
            <Money minor={item.amountMinor} intent="muted" className="text-[13.5px]" />
          </GhostCard>
        ))}
      </div>
    </section>
  )
}
