/**
 * På repeat (/recurring) — implementation-map §2.3. Two monthly-normalized
 * summary tiles + recurring template cards (cycle progress, next-post label,
 * contributing avatars, split label) with Detaljer → Recurring detail sheet and
 * Pausa/Återuppta toggling `active`. All figures are server-computed
 * (monthlyNormalized, yourShare, cycleProgress); the SPA only renders (ADR-0006).
 */
import { createFileRoute } from '@tanstack/react-router'
import { toast } from 'sonner'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/progress'
import { Skeleton } from '@/components/ui/skeleton'
import { Money } from '@/components/money'
import { MemberAvatarStack } from '@/components/member-avatar'
import { EmptyState, ErrorState } from '@/components/screen-states'
import { cn } from '@/lib/utils'
import { inDays } from '@/lib/format'
import { useActiveHousehold } from '@/lib/active-household'
import { useMembers, useRecurringList, useUpdateRecurring } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import type { MemberDto, RecurringDto } from '@/lib/api'

export const Route = createFileRoute('/recurring')({
  component: RecurringPage,
})

const CADENCE_LABEL: Record<string, string> = {
  monthly: 'Varje månad',
  biweekly: 'Varannan vecka',
  weekly: 'Varje vecka',
}

const SPLIT_LABEL: Record<string, string> = {
  equal: 'Delas lika',
  percent: 'Egna procent',
  amount: 'Egna belopp',
  none: '',
}

function cadenceLabel(cadence: string): string {
  return CADENCE_LABEL[cadence] ?? cadence
}

function perLabel(cadence: string): string {
  return cadence === 'biweekly' ? '/ 2 v' : '/ mån'
}

function RecurringPage() {
  const { householdId } = useActiveHousehold()
  const recurring = useRecurringList(householdId)
  const { data: members } = useMembers(householdId)

  return (
    <div className="space-y-4">
      <div>
        <h1 className="font-heading text-lg font-bold tracking-tight">På repeat</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Återkommande kostnader bokför sig själva — ingen månatlig inmatning.
        </p>
      </div>

      {recurring.isPending ? (
        <RecurringLoading />
      ) : recurring.isError ? (
        <ErrorState error={recurring.error} onRetry={() => void recurring.refetch()} />
      ) : recurring.data.templates.length === 0 ? (
        <EmptyState>Inget på repeat än</EmptyState>
      ) : (
        <>
          <div className="flex gap-2.5">
            <Card className="flex-1 gap-1 p-3.5">
              <Money minor={recurring.data.recTotalMinor} className="text-[15px] font-semibold" />
              <span className="text-xs text-muted-foreground">delat / månad</span>
            </Card>
            <Card className="flex-1 gap-1 border-transparent bg-accent p-3.5">
              <Money
                minor={recurring.data.recShareMinor}
                className="text-[15px] font-semibold text-accent-foreground"
              />
              <span className="text-xs text-accent-foreground/70">din del / månad</span>
            </Card>
          </div>

          <div className="flex flex-col gap-2.5">
            {recurring.data.templates.map((t) => (
              <RecurringCard key={t.id} template={t} members={members} householdId={householdId} />
            ))}
          </div>

          <p className="px-5 pb-2 text-center text-xs leading-relaxed text-muted-foreground text-balance">
            Varje period bokför Settl posten i loggboken och delar om den — ändra delningen när som
            helst, det gäller framtida perioder.
          </p>
        </>
      )}
    </div>
  )
}

function RecurringCard({
  template,
  members,
  householdId,
}: {
  template: RecurringDto
  members: MemberDto[] | undefined
  householdId: string | undefined
}) {
  const { openSheet } = useSheet()
  const updateRecurring = useUpdateRecurring(householdId)
  const toggling =
    updateRecurring.isPending && updateRecurring.variables?.recId === template.id

  const pct = template.active ? Number(template.cycleProgress) * 100 : 0
  const cadLabel = `${cadenceLabel(template.cadence)} · ${template.payerName} betalar`
  const splitLabel = SPLIT_LABEL[template.splitMode] ?? ''

  const avatars = template.contributingMemberIds
    .map((id) => members?.find((m) => m.id === id))
    .filter((m): m is MemberDto => Boolean(m))
    .map((m) => ({ name: m.name, avatarColor: m.avatarColor }))

  const onToggle = () => {
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
          toast(
            next
              ? `${template.title} återupptagen`
              : `${template.title} pausad — inga fler autobokföringar`,
          ),
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : 'Något gick fel. Försök igen.'),
      },
    )
  }

  return (
    <Card className={cn('gap-0 p-4', !template.active && 'opacity-[0.55]')}>
      <div className="flex items-start gap-3">
        <span
          aria-hidden="true"
          className="grid size-9 shrink-0 place-items-center rounded-xl bg-accent text-base font-bold text-accent-foreground"
        >
          ↻
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-semibold">{template.title}</p>
          <p className="truncate text-xs text-muted-foreground">{cadLabel}</p>
        </div>
        <div className="shrink-0 text-right">
          <Money minor={template.amountMinor} className="text-sm font-semibold" />
          <span className="block text-[10.5px] text-muted-foreground">
            {perLabel(template.cadence)}
          </span>
        </div>
      </div>

      <Progress value={pct} className="mt-3 mb-2 h-[5px]" />

      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-semibold text-accent-foreground">
          {template.active ? `Bokförs ${inDays(template.nextPostDate)}` : 'Pausad'}
        </span>
        <span className="flex items-center gap-2">
          {avatars.length > 0 && <MemberAvatarStack members={avatars} />}
          {splitLabel && <span className="text-xs text-muted-foreground">{splitLabel}</span>}
        </span>
      </div>

      <div className="mt-3 flex gap-2 border-t border-border pt-3">
        <Button
          variant="secondary"
          size="sm"
          className="flex-1"
          onClick={() => openSheet('recurring', { id: template.id })}
        >
          Detaljer
        </Button>
        <Button
          variant="outline"
          size="sm"
          className="flex-1"
          disabled={toggling}
          onClick={onToggle}
        >
          {template.active ? 'Pausa' : 'Återuppta'}
        </Button>
      </div>
    </Card>
  )
}

function RecurringLoading() {
  return (
    <div className="space-y-4" aria-busy="true">
      <div className="flex gap-2.5">
        <Skeleton className="h-16 flex-1 rounded-2xl" />
        <Skeleton className="h-16 flex-1 rounded-2xl" />
      </div>
      <div className="flex flex-col gap-2.5">
        <Skeleton className="h-40 w-full rounded-2xl" />
        <Skeleton className="h-40 w-full rounded-2xl" />
      </div>
    </div>
  )
}
