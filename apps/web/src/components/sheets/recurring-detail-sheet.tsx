/**
 * Recurring detail overlay — implementation-map §2.9. Rendered by SheetRouter
 * when `sheet==='recurring'`. Shows the template's next auto-post, how it splits
 * (frozen shares), previous posted periods, and a pause/resume toggle. All state
 * (shares, posted entries, active) comes from the API (ADR-0006).
 */
import { toast } from 'sonner'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Money } from '@/components/money'
import { GhostCard } from '@/components/ghost-card'
import { MemberAvatar } from '@/components/member-avatar'
import { LoadingState, ErrorState } from '@/components/screen-states'
import { cn } from '@/lib/utils'
import { inDays, shortDate } from '@/lib/format'
import { useActiveHousehold } from '@/lib/active-household'
import { useMembers, useRecurringDetail, useUpdateRecurring } from '@/lib/queries'
import type { RecurringDetailDto } from '@/lib/api'

const CADENCE_LABEL: Record<string, string> = {
  monthly: 'Varje månad',
  biweekly: 'Varannan vecka',
  weekly: 'Varje vecka',
}

export function RecurringDetailSheet({
  open,
  onClose,
  recId,
}: {
  open: boolean
  onClose: () => void
  recId?: string
}) {
  const { householdId } = useActiveHousehold()
  const detail = useRecurringDetail(recId)
  const { data: members } = useMembers(householdId)
  const updateRecurring = useUpdateRecurring(householdId)

  const template = detail.data?.template
  const cadLabel = template
    ? `${CADENCE_LABEL[template.cadence] ?? template.cadence} · ${template.payerName} betalar`
    : ''

  const onToggle = () => {
    if (!template) return
    const next = !template.active
    updateRecurring.mutate(
      {
        recId: template.id,
        body: {
          active: next,
          title: null,
          amountMinor: null,
          cadence: null,
          nextPostDate: null,
          paidByMemberId: null,
          split: null,
        },
      },
      {
        onSuccess: () =>
          toast(next ? `${template.title} återupptagen` : `${template.title} pausad`),
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : 'Något gick fel. Försök igen.'),
      },
    )
  }

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title={template ? template.title : 'På repeat'}
    >
      {detail.isPending ? (
        <LoadingState rows={3} />
      ) : detail.isError ? (
        <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />
      ) : (
        <RecurringDetailBody
          detail={detail.data}
          colorOf={(id) => members?.find((m) => m.id === id)?.avatarColor ?? ''}
          emojiOf={(id) => members?.find((m) => m.id === id)?.avatarEmoji ?? null}
          cadLabel={cadLabel}
          toggling={updateRecurring.isPending}
          onToggle={onToggle}
        />
      )}
    </ResponsiveSheet>
  )
}

function RecurringDetailBody({
  detail,
  colorOf,
  emojiOf,
  cadLabel,
  toggling,
  onToggle,
}: {
  detail: RecurringDetailDto
  colorOf: (id: string) => string
  emojiOf: (id: string) => string | null
  cadLabel: string
  toggling: boolean
  onToggle: () => void
}) {
  const { template, shares, postedEntries } = detail

  const nextTitle = template.active
    ? `Nästa: ${template.title} — ${shortDate(template.nextPostDate)}`
    : 'Pausad — ingen kommande bokföring'
  const nextWhen = template.active
    ? `Bokförs automatiskt ${inDays(template.nextPostDate)}`
    : 'Återuppta för att schemalägga nästa period'

  return (
    <div className="flex flex-col">
      <div className="flex items-center gap-2">
        <Badge variant="secondary" className="uppercase tracking-wide">
          På repeat
        </Badge>
        <span className="text-xs text-muted-foreground">{cadLabel}</span>
      </div>

      <Money minor={template.amountMinor} className="mt-3 text-[28px] font-semibold" />

      <GhostCard className="mt-4 flex items-center gap-3">
        <span
          aria-hidden="true"
          className="grid size-9 shrink-0 place-items-center rounded-xl bg-accent text-base font-bold text-accent-foreground"
        >
          ↻
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold">{nextTitle}</p>
          <p className="text-xs font-semibold text-accent-foreground">{nextWhen}</p>
        </div>
        <div className="shrink-0 text-right">
          <Money minor={template.yourShareMinor} className="text-xs font-semibold" />
          <span className="block text-[10.5px] text-muted-foreground">din del</span>
        </div>
      </GhostCard>

      <h3 className="mt-4 mb-2 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
        Hur den delas
      </h3>
      <Card className="gap-0 px-4 py-0">
        {shares.map((s, i) => (
          <div
            key={s.memberId}
            className={cn(
              'flex items-center gap-2.5 py-2.5',
              i < shares.length - 1 && 'border-b border-border',
            )}
          >
            <MemberAvatar name={s.name} avatarColor={colorOf(s.memberId)} avatarEmoji={emojiOf(s.memberId)} size="sm" />
            <span className="flex-1 text-sm font-semibold">
              {s.name}
              {s.isPayer && (
                <span className="ml-1 text-xs font-bold text-accent-foreground">· betalar</span>
              )}
            </span>
            <Money minor={s.shareMinor} className="text-[13px]" />
          </div>
        ))}
      </Card>
      <p className="mt-2 text-xs text-muted-foreground">
        Ändrad delning gäller framtida perioder.
      </p>

      {postedEntries.length > 0 && (
        <div className="mt-4">
          <h3 className="mb-1.5 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
            Tidigare perioder
          </h3>
          {postedEntries.map((p) => (
            <div
              key={p.id}
              className="flex items-center justify-between border-b border-border py-2.5"
            >
              <span className="truncate text-[13px] font-semibold">{p.title}</span>
              <span className="flex shrink-0 items-center gap-2.5">
                <span className="text-xs text-muted-foreground">
                  {p.settled ? 'reglerad' : 'öppen'}
                </span>
                <Money minor={p.amountMinor} className="text-[12.5px]" />
              </span>
            </div>
          ))}
        </div>
      )}

      <Button variant="outline" className="mt-4 w-full" disabled={toggling} onClick={onToggle}>
        {template.active ? 'Pausa autobokföring' : 'Återuppta autobokföring'}
      </Button>
    </div>
  )
}
