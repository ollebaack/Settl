/**
 * Settle-up overlay (implementation-map §2.8). Shows the net position with one
 * person, the entries contributing to it (signed amounts), and a single action
 * that records one first-class settlement event closing the whole pair.
 * Everything (net, label, contributing entries) is DERIVED server-side via
 * useSettlePreview — the sheet never computes balances (ADR-0006).
 */
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { cn } from '@/lib/utils'
import { formatSignedKr, shortDate } from '@/lib/format'
import { useActiveHousehold } from '@/lib/active-household'
import { useCreateSettlement, useSettlePreview } from '@/lib/queries'
import type { MoneyIntent } from '@/components/money'
import { Money } from '@/components/money'
import type { SettlePreviewDto } from '@/lib/api'

/** settle-preview netLabel is Labels.Relation: owesYou | youOwe | square. */
function netCopy(label: string, name: string): string {
  if (label === 'owesYou') return `${name} är skyldig dig`
  if (label === 'youOwe') return `Du är skyldig ${name}`
  return 'Allt är kvitt'
}

function netIntent(label: string): MoneyIntent {
  if (label === 'owesYou') return 'success'
  if (label === 'youOwe') return 'destructive'
  return 'muted'
}

/** Colour for a signed contributing line: positive owed to you, negative owed. */
function signIntent(minor: number): MoneyIntent {
  if (minor > 0) return 'success'
  if (minor < 0) return 'destructive'
  return 'muted'
}

function SettleUpBody({
  preview,
  householdId,
  person,
  onClose,
}: {
  preview: SettlePreviewDto
  householdId: string
  person: string
  onClose: () => void
}) {
  const settlement = useCreateSettlement(householdId)
  const isSquare = preview.netLabel === 'square'

  const handleSettle = () => {
    if (settlement.isPending) return
    settlement.mutate(
      { personMemberId: person },
      {
        onSuccess: () => {
          toast(`Uppgjort med ${preview.memberName} — rent bord`)
          onClose()
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : 'Kunde inte göra upp'),
      },
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col items-center gap-1 text-center">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {netCopy(preview.netLabel, preview.memberName)}
        </p>
        {!isSquare && (
          <Money
            minor={preview.netMinor}
            intent={netIntent(preview.netLabel)}
            className="text-[32px] leading-none"
          />
        )}
      </div>

      {preview.entries.length > 0 && (
        <div className="flex flex-col gap-2">
          {preview.entries.map((e) => {
            const signed = typeof e.signedAmountMinor === 'number'
              ? e.signedAmountMinor
              : Number(e.signedAmountMinor)
            return (
              <Card key={e.id} size="sm" className="flex flex-row items-center gap-3 p-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{e.title}</p>
                  <p className="truncate text-xs text-muted-foreground">{shortDate(e.date)}</p>
                </div>
                <span
                  className={cn(
                    'font-mono tabular-nums text-[13.5px]',
                    signIntent(signed) === 'success' && 'text-success',
                    signIntent(signed) === 'destructive' && 'text-destructive',
                    signIntent(signed) === 'muted' && 'text-muted-foreground',
                  )}
                >
                  {formatSignedKr(e.signedAmountMinor)}
                </span>
              </Card>
            )
          })}
        </div>
      )}

      <p className="text-xs text-muted-foreground">
        Betala som ni brukar — Swish, kontanter, banköverföring. Settl håller bara boken i
        ordning.
      </p>

      {!isSquare && (
        <Button onClick={handleSettle} disabled={settlement.isPending} className="w-full">
          Markera allt som reglerat
        </Button>
      )}
    </div>
  )
}

export function SettleUpSheet({
  open,
  onClose,
  person,
}: {
  open: boolean
  onClose: () => void
  person?: string
}) {
  const { householdId } = useActiveHousehold()
  const query = useSettlePreview(open ? householdId : undefined, open ? person : undefined)
  const preview = query.data

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title={preview ? `Gör upp med ${preview.memberName}` : 'Gör upp'}
    >
      {query.isLoading ? (
        <LoadingState rows={3} />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : preview && householdId && person ? (
        <SettleUpBody
          preview={preview}
          householdId={householdId}
          person={person}
          onClose={onClose}
        />
      ) : null}
    </ResponsiveSheet>
  )
}
