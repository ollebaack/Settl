import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { BellIcon, ReceiptTextIcon, RepeatIcon, TrendingUpIcon, type LucideIcon } from 'lucide-react'
import { RequireAuth } from '@/components/require-auth'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ErrorState, LoadingState, NoHouseholdState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'
import { useNudges, useNudgeTone } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import type { NudgeActionDto, NudgeDto } from '@/lib/api'

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

// The three event triggers (implementation-map §2.4 / ambiguity #5). Rendered as a
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
    <div className="flex flex-col gap-4">
      <header>
        <h1 className="font-heading text-[19px] font-bold tracking-tight">Notiser</h1>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">
          Settl säger bara till när något händer.
        </p>
      </header>

      <ActivityBody
        householdId={householdId}
        query={nudgesQuery}
        onAction={runAction}
      />

      <NudgeTriggers />
    </div>
  )
}

/** Scannable reference for the three events that produce a notification. */
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
