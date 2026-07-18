import { useEffect, useRef } from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import {
  BellIcon,
  CheckCircle2Icon,
  PencilLineIcon,
  ReceiptTextIcon,
  RepeatIcon,
  Trash2Icon,
  TrendingUpIcon,
  type LucideIcon,
} from 'lucide-react'
import { RequireAuth } from '@/components/require-auth'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ErrorState, LoadingState, NoHouseholdState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'
import {
  useNotifications,
  useMarkNotificationsSeen,
  useNudges,
  useNudgeTone,
} from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import { formatKr, inDays } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { NotificationDto, NudgeActionDto, NudgeDto } from '@/lib/api'

export const Route = createFileRoute('/activity')({
  component: () => (
    <RequireAuth>
      <ActivityPage />
    </RequireAuth>
  ),
})

// Tone comes from the member's profile setting; direct is the default (implementation-map
// §2.4, ambiguity #18). Nudge title/body text is localised (tone-selected) by the API and
// rendered as-is.

// The three nudge triggers (implementation-map §2.4 / ambiguity #5). Rendered as a
// scannable "why do I get these" reference instead of the old run-on footer sentence.
const TRIGGERS: { icon: LucideIcon; title: string; detail: string }[] = [
  { icon: ReceiptTextIcon, title: 'En stor utgift läggs till', detail: 'Från 1500 kr och uppåt.' },
  { icon: TrendingUpIcon, title: 'Ett saldo blir stort', detail: 'När det passerar 750 kr.' },
  {
    icon: RepeatIcon,
    title: 'En återkommande kostnad närmar sig',
    detail: 'Några dagar innan den bokförs.',
  },
]

function ActivityPage() {
  const { householdId, households, isLoading: householdsLoading } = useActiveHousehold()
  const { openSheet } = useSheet()
  const navigate = useNavigate()
  const nudgesQuery = useNudges(householdId, useNudgeTone())

  if (!householdsLoading && households.length === 0) {
    return <NoHouseholdState onCreate={() => openSheet('newHousehold')} className="mt-6" />
  }

  // Dispatch a nudge action to its overlay (implementation-map §2.4 / §4).
  function runAction(action: NudgeActionDto) {
    switch (action.kind) {
      case 'viewEntry':
        openSheet('entry', { id: action.targetId })
        break
      case 'viewRecurring':
        // Recurring-due nudge → jump to /recurring and open its detail sheet.
        navigate({
          to: '/recurring',
          search: { sheet: 'recurring', id: action.targetId },
        })
        break
      case 'settle':
        openSheet('settle', { person: action.targetId })
        break
    }
  }

  return (
    <div className="flex flex-col gap-5">
      <header>
        <h1 className="font-heading text-[19px] font-bold tracking-tight">Notiser</h1>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">
          Settl säger bara till när något händer.
        </p>
      </header>

      <TrustNotifications
        householdId={householdId}
        onOpenEntry={(id) => openSheet('entry', { id })}
        onOpenRecurring={(id) =>
          navigate({ to: '/recurring', search: { sheet: 'recurring', id } })
        }
      />

      <section className="flex flex-col gap-2.5">
        <SectionLabel>Att ta tag i</SectionLabel>
        <ActivityBody householdId={householdId} query={nudgesQuery} onAction={runAction} />
      </section>

      <NudgeTriggers />
    </div>
  )
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
      {children}
    </p>
  )
}

/** The trust stream: changes other members made that affect your money (ADR-0028). Opening
 *  the screen marks them seen, but dots stay for what was unread when this visit began. */
function TrustNotifications({
  householdId,
  onOpenEntry,
  onOpenRecurring,
}: {
  householdId: string | undefined
  onOpenEntry: (entryId: string) => void
  onOpenRecurring: (recurringId: string) => void
}) {
  const query = useNotifications(householdId)
  const markSeen = useMarkNotificationsSeen(householdId)

  // Snapshot which ids were unread when the screen first loaded, so their dots persist for the
  // whole visit even after the cursor advances and the list refetches as read.
  const unreadAtOpen = useRef<Set<string> | null>(null)
  const markedRef = useRef(false)
  const list = query.data
  useEffect(() => {
    if (!list || markedRef.current) return
    unreadAtOpen.current = new Set(
      list.items.filter((n) => n.isUnread).map((n) => n.id),
    )
    if (Number(list.unreadCount) > 0) {
      markedRef.current = true
      markSeen.mutate()
    }
  }, [list, markSeen])

  if (!householdId || query.isPending) return <LoadingState rows={1} />
  if (query.isError) return <ErrorState error={query.error} onRetry={() => query.refetch()} />

  const items = query.data?.items ?? []
  if (items.length === 0) return null // empty trust stream shows nothing; nudges carry the empty state

  return (
    <section className="flex flex-col gap-2.5">
      <SectionLabel>Ändringar som rör dig</SectionLabel>
      <div className="flex flex-col gap-2.5">
        {items.map((n) => (
          <NotificationCard
            key={n.id}
            notification={n}
            wasUnread={unreadAtOpen.current?.has(n.id) ?? n.isUnread}
            onOpenEntry={onOpenEntry}
            onOpenRecurring={onOpenRecurring}
          />
        ))}
      </div>
    </section>
  )
}

const NOTIFICATION_ICON: Record<NotificationDto['type'], LucideIcon> = {
  entryDeleted: Trash2Icon,
  entryEdited: PencilLineIcon,
  settlementRecorded: CheckCircle2Icon,
  recurringChanged: RepeatIcon,
}

/** Headline built from the structured event (render layer — no copy is baked server-side).
 *  `type` is a loose string on the wire, so edited/recurring share the default branch. */
function headline(n: NotificationDto): string {
  const subject = `”${n.title}”`
  switch (n.type) {
    case 'entryDeleted':
      return `${n.actorName} tog bort ${subject}`
    case 'settlementRecorded':
      return `${n.actorName} markerade ${subject} som betald`
    default:
      return `${n.actorName} ändrade ${subject}`
  }
}

function NotificationCard({
  notification: n,
  wasUnread,
  onOpenEntry,
  onOpenRecurring,
}: {
  notification: NotificationDto
  wasUnread: boolean
  onOpenEntry: (entryId: string) => void
  onOpenRecurring: (recurringId: string) => void
}) {
  const Icon = NOTIFICATION_ICON[n.type] ?? BellIcon

  // A deleted entry is gone — nothing to open. Everything else still has a target.
  const target =
    n.type === 'recurringChanged' && n.recurringTemplateId
      ? () => onOpenRecurring(n.recurringTemplateId!)
      : n.type !== 'entryDeleted' && n.entryId
        ? () => onOpenEntry(n.entryId!)
        : undefined

  const body = (
    <div className="flex items-start gap-2.5 px-(--card-spacing)">
      <span
        aria-hidden="true"
        className={cn(
          'mt-0.5 grid size-7 flex-none place-items-center rounded-full',
          wasUnread ? 'bg-primary/12 text-primary' : 'bg-muted text-muted-foreground',
        )}
      >
        <Icon className="size-3.5" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline justify-between gap-2">
          <span className="text-[13.5px] font-[650] text-foreground">{headline(n)}</span>
          <span className="flex flex-none items-center gap-1.5 text-[10.5px] text-muted-foreground">
            {wasUnread && (
              <span className="size-1.5 rounded-full bg-primary" aria-label="Oläst" />
            )}
            {inDays(n.occurredAt)}
          </span>
        </div>

        {n.changes.length > 0 ? (
          <ul className="mt-1.5 flex flex-col gap-1">
            {n.changes.map((c) => (
              <li key={c.field} className="text-[12.5px] leading-snug text-muted-foreground">
                <span className="text-foreground/70">{c.label}:</span>{' '}
                <span className="line-through decoration-muted-foreground/40">{c.before}</span>
                {' → '}
                <span className="font-[600] text-foreground">{c.after}</span>
              </li>
            ))}
          </ul>
        ) : (
          n.amountMinor != null && (
            <p className="mt-1 text-[12.5px] text-muted-foreground">
              Var på {formatKr(n.amountMinor)}
            </p>
          )
        )}
      </div>
    </div>
  )

  return (
    <Card size="sm" className="gap-0">
      {target ? (
        <button type="button" onClick={target} className="w-full py-0.5 text-left">
          {body}
        </button>
      ) : (
        body
      )}
    </Card>
  )
}

/** Scannable reference for the three events that produce a nudge. */
function NudgeTriggers() {
  return (
    <Card size="sm" className="gap-0">
      <p className="px-(--card-spacing) text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
        Du hör av oss när
      </p>
      <ul className="mt-3 flex flex-col gap-3 px-(--card-spacing)">
        {TRIGGERS.map(({ icon: Icon, title, detail }) => (
          <li key={title} className="flex items-start gap-3">
            <span
              aria-hidden="true"
              className="grid size-7 flex-none place-items-center rounded-full bg-muted text-muted-foreground"
            >
              <Icon className="size-3.5" />
            </span>
            <div className="min-w-0">
              <p className="text-[13px] font-[650] text-foreground">{title}</p>
              <p className="text-[12px] leading-snug text-muted-foreground">{detail}</p>
            </div>
          </li>
        ))}
      </ul>
    </Card>
  )
}

function ActivityBody({
  householdId,
  query,
  onAction,
}: {
  householdId: string | undefined
  query: ReturnType<typeof useNudges>
  onAction: (action: NudgeActionDto) => void
}) {
  // Guard while the active household is still resolving.
  if (!householdId || query.isPending) {
    return <LoadingState rows={2} />
  }

  if (query.isError) {
    return <ErrorState error={query.error} onRetry={() => query.refetch()} />
  }

  const nudges = query.data ?? []
  if (nudges.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 px-6 py-10 text-center">
        <span
          aria-hidden="true"
          className="grid size-11 place-items-center rounded-full bg-muted text-muted-foreground"
        >
          <BellIcon className="size-5" />
        </span>
        <p className="text-[13.5px] font-[650] text-foreground">Allt lugnt</p>
        <p className="text-sm text-muted-foreground">Inga notiser att ta tag i just nu.</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2.5">
      {nudges.map((nudge, i) => (
        <NudgeCard key={i} nudge={nudge} onAction={onAction} />
      ))}
    </div>
  )
}

function NudgeCard({
  nudge,
  onAction,
}: {
  nudge: NudgeDto
  onAction: (action: NudgeActionDto) => void
}) {
  return (
    <Card size="sm" className="gap-0">
      <div className="flex items-start gap-2.5 px-(--card-spacing)">
        <span
          className="mt-1.5 size-2 flex-none rounded-full bg-primary"
          aria-hidden="true"
        />
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="text-sm font-[650] text-foreground">{nudge.title}</span>
            <span className="flex-none text-[10.5px] text-muted-foreground">{nudge.when}</span>
          </div>
          <p className="mt-1 text-[12.5px] leading-relaxed text-muted-foreground">
            {nudge.body}
          </p>
          {nudge.actions.length > 0 && (
            <div className="mt-2.5 flex flex-wrap gap-2">
              {nudge.actions.map((action, i) => (
                <Button
                  key={i}
                  variant="secondary"
                  size="sm"
                  onClick={() => onAction(action)}
                >
                  {action.label}
                </Button>
              ))}
            </div>
          )}
        </div>
      </div>
    </Card>
  )
}
