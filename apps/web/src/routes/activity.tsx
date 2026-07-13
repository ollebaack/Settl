import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { RequireAuth } from '@/components/require-auth'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { EmptyState, ErrorState, LoadingState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'
import { useNudges } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import type { NudgeActionDto, NudgeDto, NudgeTone } from '@/lib/api'

export const Route = createFileRoute('/activity')({
  component: () => (
    <RequireAuth>
      <ActivityPage />
    </RequireAuth>
  ),
})

// Tone comes from a setting; direct is the default (implementation-map §2.4,
// ambiguity #18). Nudge title/body text is localised (tone-selected) by the API
// and rendered as-is.
const TONE: NudgeTone = 'direct'

const EMPTY_COPY =
  'Lugnt just nu. Settl knuffar när en stor utgift läggs till, ett saldo växer eller hyran närmar sig.'
const FOOTER_COPY =
  'Inget dagligt tjat — knuffar skickas vid händelser: en stor utgift, ett saldo som passerar en gräns eller en återkommande kostnad som snart bokförs.'

function ActivityPage() {
  const { householdId } = useActiveHousehold()
  const { openSheet } = useSheet()
  const navigate = useNavigate()
  const nudgesQuery = useNudges(householdId, TONE)

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
        <h1 className="font-heading text-[19px] font-bold tracking-tight">Knuffar</h1>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">
          Settl säger bara till när något händer.
        </p>
      </header>

      <ActivityBody
        householdId={householdId}
        query={nudgesQuery}
        onAction={runAction}
      />

      <p className="px-5 text-center text-xs leading-relaxed text-muted-foreground text-balance">
        {FOOTER_COPY}
      </p>
    </div>
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
    return <EmptyState className="py-12">{EMPTY_COPY}</EmptyState>
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
