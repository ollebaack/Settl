/**
 * Entry detail overlay (implementation-map §2.7). Inspects one ledger entry:
 * type badge + date, title, amount, payer/split meta (or the informal-loan
 * note for IOUs), an optional auto-post note, per-member share rows (a two-row
 * from/to view for IOUs), and a settle/reopen toggle. Settled state is DERIVED
 * server-side (EntryDto.settled) — never computed here (ADR-0006). All server
 * state comes from the shared TanStack Query hooks.
 */
import { useState } from 'react'
import { MoreHorizontalIcon, PencilIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { ConfirmDialog, WarnBox } from '@/components/confirm-dialog'
import { Money } from '@/components/money'
import { MemberAvatar } from '@/components/member-avatar'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { formatKr, shortDate } from '@/lib/format'
import { CATEGORY_ICON, CATEGORY_LABEL, CATEGORY_ORDER } from '@/lib/categories'
import { useSheet } from '@/lib/sheet'
import { useDeferredDeleteEntry } from '@/lib/use-entry-delete'
import { useEntry, useMe, useMembers, useReopenEntry, useSettleEntry, useUpdateEntry } from '@/lib/queries'
import type { EntryCategory, EntryDto, EntryType, SplitModeName } from '@/lib/api'

const typeBadge: Record<EntryType, string> = {
  expense: 'Utgift',
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
  avatarEmoji?: string | null
  tag: string
  minor: number | string
}

/** Rows for the share list: one row per member's frozen share. */
function buildShareRows(entry: EntryDto): ShareRowData[] {
  return entry.shares.map((s) => ({
    key: s.memberId,
    name: s.name,
    avatarColor: s.avatarColor,
    avatarEmoji: s.avatarEmoji,
    tag: s.isPayer ? '· betalade' : '· skyldig',
    minor: s.shareMinor,
  }))
}

/** Tap-to-change category affordance for expense entries only (entry-categories spec). */
function CategoryPicker({ entry }: { entry: EntryDto }) {
  const update = useUpdateEntry(entry.householdId)
  const category = entry.category as EntryCategory
  const Icon = CATEGORY_ICON[category]

  const handleSelect = (next: EntryCategory) => {
    if (next === category || update.isPending) return
    update.mutate(
      {
        entryId: entry.id,
        body: {
          type: entry.type,
          title: entry.title,
          amountMinor: entry.amountMinor,
          date: entry.date,
          paidByMemberId: entry.paidByMemberId,
          split: null,
          category: next,
        },
      },
      { onError: (err) => toast.error(err instanceof Error ? err.message : 'Kunde inte ändra kategori') },
    )
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <button
            type="button"
            aria-label={`Kategori: ${CATEGORY_LABEL[category]} — tryck för att ändra`}
            className="flex items-center gap-1.5 rounded-full bg-muted px-2 py-1 text-xs font-medium text-muted-foreground transition-colors hover:bg-muted/70"
          />
        }
      >
        <Icon className="size-3.5" strokeWidth={1.8} />
        {CATEGORY_LABEL[category]}
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start">
        {CATEGORY_ORDER.map((c) => {
          const ItemIcon = CATEGORY_ICON[c]
          return (
            <DropdownMenuItem key={c} onClick={() => handleSelect(c)}>
              <ItemIcon className="size-4" strokeWidth={1.8} />
              {CATEGORY_LABEL[c]}
            </DropdownMenuItem>
          )
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function EntryDetailBody({ entry, onClose }: { entry: EntryDto; onClose: () => void }) {
  const { data: me } = useMe()
  const viewerId = me?.id
  const { data: members } = useMembers(entry.householdId)
  const settle = useSettleEntry(entry.householdId)
  const reopen = useReopenEntry(entry.householdId)
  const { openSheet } = useSheet()
  const deferredDelete = useDeferredDeleteEntry(entry.householdId)
  const [confirmOpen, setConfirmOpen] = useState(false)

  const handleDelete = () => {
    setConfirmOpen(false)
    onClose()
    deferredDelete({ id: entry.id })
  }

  const nameOf = (id: string | null | undefined): string => {
    if (!id) return ''
    return (
      entry.shares.find((s) => s.memberId === id)?.name ??
      members?.find((m) => m.id === id)?.name ??
      ''
    )
  }

  const paidByViewer = viewerId != null && entry.paidByMemberId === viewerId
  const payerMeta = paidByViewer ? 'Du betalade' : `${nameOf(entry.paidByMemberId)} betalade`
  const split = splitLabel[entry.splitMode as SplitModeName] ?? ''
  const meta = [payerMeta, split].filter(Boolean).join(' · ')

  const rows = buildShareRows(entry)
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
        {entry.type === 'expense' && <CategoryPicker entry={entry} />}
        <span>{shortDate(entry.date)}</span>

        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <button
                type="button"
                aria-label="Fler åtgärder"
                className="ml-auto grid size-8 place-items-center rounded-full bg-muted text-muted-foreground transition-colors hover:bg-muted/70"
              />
            }
          >
            <MoreHorizontalIcon className="size-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem
              disabled={entry.locked}
              onClick={() => openSheet('edit', { id: entry.id })}
            >
              <PencilIcon />
              Redigera
            </DropdownMenuItem>
            <DropdownMenuItem
              variant="destructive"
              disabled={entry.locked}
              onClick={() => setConfirmOpen(true)}
            >
              <Trash2Icon />
              Ta bort
            </DropdownMenuItem>
            {entry.locked && (
              <p className="px-3 pt-1 pb-1.5 text-xs text-muted-foreground">
                Låst — öppna igen för att ändra eller ta bort.
              </p>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
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
              <MemberAvatar name={row.name} avatarColor={row.avatarColor} avatarEmoji={row.avatarEmoji} size="sm" />
              <div className="min-w-0 flex-1 truncate text-sm">
                <span className="font-medium">{row.name}</span>{' '}
                <span className="font-bold text-accent-foreground">{row.tag}</span>
              </div>
              <Money minor={row.minor} className="text-[13.5px]" />
            </Card>
          ))}
        </div>
      )}

      {entry.locked && (
        <WarnBox
          heading="Posten är låst"
          body="En uppgörelse rör den här posten. Öppna igen innan du ändrar eller tar bort — då rörs bara den här posten."
        />
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

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Ta bort posten?"
        confirmLabel="Ta bort"
        destructive
        onConfirm={handleDelete}
      >
        <WarnBox
          heading="Det här ändrar saldot för alla som var med"
          body={
            <>
              ”{entry.title}” · {formatKr(entry.amountMinor)} tas bort ur loggboken. Du kan
              ångra direkt efteråt.
            </>
          }
        />
        <p className="text-xs text-muted-foreground">
          Borttagningen görs efter några sekunder — hinner du ångra händer inget.
        </p>
      </ConfirmDialog>
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
        <EntryDetailBody entry={entry} onClose={onClose} />
      ) : null}
    </ResponsiveSheet>
  )
}
