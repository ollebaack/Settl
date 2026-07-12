/**
 * Entry detail overlay (implementation-map §2.7). Inspects one ledger entry:
 * type badge + date, title, amount, payer/split meta (or the informal-loan
 * note for IOUs), an optional auto-post note, per-member share rows (a two-row
 * from/to view for IOUs), and a settle/reopen toggle. Settled state is DERIVED
 * server-side (EntryDto.settled) — never computed here (ADR-0006). All server
 * state comes from the shared TanStack Query hooks.
 */
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Money } from '@/components/money'
import { MemberAvatar } from '@/components/member-avatar'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { shortDate } from '@/lib/format'
import { useEntry, useMembers, useReopenEntry, useSettleEntry } from '@/lib/queries'
import { useDevUser } from '@/lib/dev-user'
import type { EntryDto, EntryType, MemberDto, SplitModeName } from '@/lib/api'

const typeBadge: Record<EntryType, string> = {
  expense: 'Utgift',
  iou: 'Lån',
  recurringPost: 'På repeat',
}

const splitLabel: Record<SplitModeName, string> = {
  equal: 'Delas lika',
  percent: 'Egna procent',
  amount: 'Egna belopp',
  none: '',
}

interface ShareRowData {
  key: string
  name: string
  avatarColor: string
  tag: string
  minor: number | string
}

/** Rows for the share list: two-row from/to for IOUs, per-member otherwise. */
function buildShareRows(entry: EntryDto, members: MemberDto[]): ShareRowData[] {
  const memberById = (id: string | null | undefined): MemberDto | undefined =>
    id ? members.find((m) => m.id === id) : undefined

  if (entry.type === 'iou') {
    const from = memberById(entry.fromMemberId)
    const to = memberById(entry.toMemberId)
    const rows: ShareRowData[] = []
    if (from) {
      rows.push({
        key: `from-${from.id}`,
        name: from.name,
        avatarColor: from.avatarColor,
        tag: '· skyldig',
        minor: entry.amountMinor,
      })
    }
    if (to) {
      rows.push({
        key: `to-${to.id}`,
        name: to.name,
        avatarColor: to.avatarColor,
        tag: '· ska få',
        minor: entry.amountMinor,
      })
    }
    return rows
  }

  return entry.shares.map((s) => ({
    key: s.memberId,
    name: s.name,
    avatarColor: s.avatarColor,
    tag: s.isPayer ? '· betalade' : '· skyldig',
    minor: s.shareMinor,
  }))
}

function EntryDetailBody({ entry }: { entry: EntryDto }) {
  const { memberId: viewerId } = useDevUser()
  const { data: members } = useMembers(entry.householdId)
  const settle = useSettleEntry(entry.householdId)
  const reopen = useReopenEntry(entry.householdId)

  const nameOf = (id: string | null | undefined): string => {
    if (!id) return ''
    return (
      entry.shares.find((s) => s.memberId === id)?.name ??
      members?.find((m) => m.id === id)?.name ??
      ''
    )
  }

  const isIou = entry.type === 'iou'
  const paidByViewer = viewerId != null && entry.paidByMemberId === viewerId
  const payerMeta = paidByViewer ? 'Du betalade' : `${nameOf(entry.paidByMemberId)} betalade`
  const split = splitLabel[entry.splitMode as SplitModeName] ?? ''
  const meta = isIou
    ? 'Informellt lån — inget kvitto behövs'
    : [payerMeta, split].filter(Boolean).join(' · ')

  const rows = buildShareRows(entry, members ?? [])
  const busy = settle.isPending || reopen.isPending

  const handleToggle = () => {
    if (busy) return
    if (entry.settled) {
      reopen.mutate(entry.id, {
        onSuccess: () => toast('Öppnad igen'),
        onError: (err) => toast.error(err instanceof Error ? err.message : 'Kunde inte öppna igen'),
      })
    } else {
      settle.mutate(entry.id, {
        onSuccess: () => toast('Reglerad — snyggt'),
        onError: (err) => toast.error(err instanceof Error ? err.message : 'Kunde inte reglera'),
      })
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <Badge variant="secondary">{typeBadge[entry.type as EntryType]}</Badge>
        <span>{shortDate(entry.date)}</span>
      </div>

      <div className="flex flex-col gap-1">
        <Money minor={entry.amountMinor} className="text-[28px] leading-none" />
        <p className="text-sm text-muted-foreground">{meta}</p>
      </div>

      {entry.recurringTemplateId && entry.templateTitle && (
        <p className="text-xs font-semibold text-accent-foreground">
          Bokförd automatiskt från ”{entry.templateTitle}”
        </p>
      )}

      {rows.length > 0 && (
        <div className="flex flex-col gap-2">
          {rows.map((row) => (
            <Card key={row.key} size="sm" className="flex flex-row items-center gap-3 p-3">
              <MemberAvatar name={row.name} avatarColor={row.avatarColor} size="sm" />
              <div className="min-w-0 flex-1 truncate text-sm">
                <span className="font-medium">{row.name}</span>{' '}
                <span className="font-bold text-accent-foreground">{row.tag}</span>
              </div>
              <Money minor={row.minor} className="text-[13.5px]" />
            </Card>
          ))}
        </div>
      )}

      <Button
        onClick={handleToggle}
        disabled={busy}
        variant={entry.settled ? 'secondary' : 'default'}
        className="w-full"
      >
        {entry.settled ? 'Reglerad ✓ — tryck för att öppna igen' : 'Markera som reglerad'}
      </Button>

      <p className="text-center text-xs text-muted-foreground">
        Betala varandra hur ni vill — markera sen som reglerad här.
      </p>
    </div>
  )
}

export function EntryDetailSheet({
  open,
  onClose,
  entryId,
}: {
  open: boolean
  onClose: () => void
  entryId?: string
}) {
  const query = useEntry(open ? entryId : undefined)
  const entry = query.data

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title={entry?.title ?? 'Post'}
    >
      {query.isLoading ? (
        <LoadingState rows={3} />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : entry ? (
        <EntryDetailBody entry={entry} />
      ) : null}
    </ResponsiveSheet>
  )
}
