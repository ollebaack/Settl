/**
 * Household switcher overlay (implementation-map §2.5). Lists every household
 * the acting user belongs to (many-to-many) with its net position, highlights
 * the active book, and switches on tap. Net + label are DERIVED server-side
 * (HouseholdListItemDto) — the sheet never computes balances (ADR-0006).
 */
import { toast } from 'sonner'
import { useNavigate } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Money } from '@/components/money'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { cn } from '@/lib/utils'
import { useActiveHousehold } from '@/lib/active-household'
import { useHouseholds } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import type { MoneyIntent } from '@/components/money'
import type { HouseholdListItemDto } from '@/lib/api'

/** household-list netLabel is Labels.Net: owed | owe | square. */
function netSub(label: string): string {
  if (label === 'owed') return 'du ska få'
  if (label === 'owe') return 'du är skyldig'
  return 'kvitt'
}

function netIntent(label: string): MoneyIntent {
  if (label === 'owed') return 'success'
  if (label === 'owe') return 'destructive'
  return 'muted'
}

export function HouseholdSwitcherSheet({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
  const navigate = useNavigate()
  const { householdId, setHouseholdId } = useActiveHousehold()
  const { openSheet } = useSheet()
  const query = useHouseholds()
  const households = query.data ?? []

  const select = (h: HouseholdListItemDto) => {
    setHouseholdId(h.id)
    // Reset back to Home with a clean overlay/search state.
    navigate({ to: '/', search: {} })
    toast(`Bytte till ${h.name}`)
  }

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Dina hushåll"
      description="En bok per hushåll — du kan vara med i flera."
    >
      {query.isLoading ? (
        <LoadingState rows={3} />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2">
            {households.map((h) => {
              const active = h.id === householdId
              return (
                <Card
                  key={h.id}
                  role="button"
                  tabIndex={0}
                  onClick={() => select(h)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault()
                      select(h)
                    }
                  }}
                  size="sm"
                  className={cn(
                    'flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring',
                    active && 'ring-2 ring-primary',
                  )}
                >
                  <span
                    aria-hidden="true"
                    className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-accent text-sm font-medium text-accent-foreground"
                  >
                    {(h.name.trim()[0] ?? '?').toUpperCase()}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{h.name}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {h.memberNames.join(', ')}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-col items-end">
                    <Money minor={h.netMinor} intent={netIntent(h.netLabel)} className="text-[13.5px]" />
                    <span className="text-xs text-muted-foreground">{netSub(h.netLabel)}</span>
                  </div>
                </Card>
              )
            })}
          </div>

          <Button
            variant="outline"
            onClick={() => openSheet('newHousehold')}
            className="w-full border-dashed"
          >
            + Nytt hushåll
          </Button>
        </div>
      )}
    </ResponsiveSheet>
  )
}
