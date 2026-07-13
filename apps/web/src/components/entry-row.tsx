/**
 * A single ledger entry row (Home "Senaste" + all Ledger rows). Card + glyph
 * tile + meta line + mono amount with a colour-coded, viewer-relative
 * sub-status. The whole row is a button that opens the entry detail sheet.
 * All status/sign derivation comes from the API (EntryDto.viewerStatus) — the
 * row never computes settled state (ADR-0006). See implementation-map §2.
 */
import { Card } from '@/components/ui/card'
import { Money } from '@/components/money'
import { cn } from '@/lib/utils'
import { formatKr, shortDate } from '@/lib/format'
import { useMe } from '@/lib/queries'
import type { EntryDto, MemberDto, ViewerStatusKind } from '@/lib/api'

type NameLookup = Pick<MemberDto, 'id' | 'name'>[]

const statusText: Record<ViewerStatusKind, (amount: string) => string> = {
  settled: () => 'reglerad',
  youOwe: (a) => `du är skyldig ${a}`,
  youAreOwed: (a) => `du ska få ${a}`,
  partiallySettled: () => 'din del reglerad',
  notYourShare: () => 'inte din del',
}

const statusClass: Record<ViewerStatusKind, string> = {
  settled: 'text-muted-foreground',
  youOwe: 'text-destructive',
  youAreOwed: 'text-success',
  partiallySettled: 'text-muted-foreground',
  notYourShare: 'text-muted-foreground',
}

function Glyph({ entry }: { entry: EntryDto }) {
  const isIou = entry.type === 'iou'
  const isRecurring = entry.type === 'recurringPost'
  const glyph = isIou ? '⇄' : isRecurring ? '↻' : entry.title.trim()[0]?.toUpperCase() || '·'
  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex size-9 shrink-0 items-center justify-center rounded-xl text-sm font-medium',
        isRecurring ? 'bg-accent text-accent-foreground' : 'bg-muted text-muted-foreground',
      )}
    >
      {glyph}
    </span>
  )
}

function buildMeta(
  entry: EntryDto,
  viewerId: string | undefined,
  nameOf: (id: string | null | undefined) => string,
): string {
  if (entry.type === 'iou') {
    if (viewerId && entry.toMemberId === viewerId) return `Lån till ${nameOf(entry.fromMemberId)}`
    if (viewerId && entry.fromMemberId === viewerId) return `Lån från ${nameOf(entry.toMemberId)}`
    return 'Lån'
  }
  const paidByViewer = viewerId != null && entry.paidByMemberId === viewerId
  if (entry.type === 'recurringPost') {
    const payer = paidByViewer ? 'du betalar' : `${nameOf(entry.paidByMemberId)} betalar`
    return `Bokförd automatiskt · ${payer} · ${shortDate(entry.date)}`
  }
  return paidByViewer ? 'Du betalade' : `${nameOf(entry.paidByMemberId)} betalade`
}

export function EntryRow({
  entry,
  members,
  onOpen,
  className,
}: {
  entry: EntryDto
  members?: NameLookup
  /** Override the default "open entry sheet" behaviour. */
  onOpen?: (entryId: string) => void
  className?: string
}) {
  const { data: me } = useMe()
  const viewerId = me?.id

  const nameOf = (id: string | null | undefined): string => {
    if (!id) return ''
    const fromShares = entry.shares.find((s) => s.memberId === id)?.name
    return fromShares ?? members?.find((m) => m.id === id)?.name ?? ''
  }

  const meta = buildMeta(entry, viewerId, nameOf)
  const status = entry.viewerStatus.kind as ViewerStatusKind
  const sub = statusText[status]?.(formatKr(entry.viewerStatus.amountMinor)) ?? ''

  return (
    <Card
      role="button"
      tabIndex={0}
      onClick={() => onOpen?.(entry.id)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onOpen?.(entry.id)
        }
      }}
      className={cn(
        'flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring',
        entry.settled && 'opacity-50',
        className,
      )}
    >
      <Glyph entry={entry} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{entry.title}</p>
        <p className="truncate text-xs text-muted-foreground">{meta}</p>
      </div>
      <div className="flex shrink-0 flex-col items-end">
        <Money minor={entry.amountMinor} className="text-[13.5px]" />
        {sub && <span className={cn('text-xs', statusClass[status])}>{sub}</span>}
      </div>
    </Card>
  )
}
