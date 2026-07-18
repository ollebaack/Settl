/**
 * Lämna hushåll (docs/specs/household-ownership.md, design frames 3 & 6).
 * A non-owner leaves; a sole owner's "leave" archives instead (RemovalPreviewDto.soleMember
 * → LeaveResultDto.archived). Open debts warn but never block. Debt figures and guard flags
 * are DERIVED server-side via useRemovalPreview — the sheet renders and calls (ADR-0006).
 */
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'

import { Money } from '@/components/money'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { Button } from '@/components/ui/button'
import { useHousehold, useLeaveHousehold, useRemovalPreview } from '@/lib/queries'
import type { RemovalPreviewDto } from '@/lib/api'

const DANGER_BTN = 'w-full bg-destructive text-destructive-foreground hover:bg-destructive/90'

function LeaveBody({
  householdId,
  householdName,
  preview,
  onClose,
}: {
  householdId: string
  householdName: string
  preview: RemovalPreviewDto
  onClose: () => void
}) {
  const navigate = useNavigate()
  const leave = useLeaveHousehold(householdId)

  // The owner can't leave while others remain — the management screen never opens this
  // sheet for that case, but guard defensively.
  if (preview.mustTransferFirst) {
    return (
      <div className="flex flex-col gap-3">
        <div className="rounded-2xl border border-border bg-accent p-3.5 text-sm leading-relaxed">
          Du äger hushållet. Överför ägarskapet till någon annan innan du lämnar.
        </div>
        <Button variant="outline" className="w-full" onClick={onClose}>
          Avbryt
        </Button>
      </div>
    )
  }

  const onConfirm = () => {
    if (leave.isPending) return
    leave.mutate(undefined, {
      onSuccess: (res) => {
        toast(
          res.archived
            ? `Du lämnade och arkiverade ${householdName}`
            : `Du lämnade ${householdName}`,
        )
        onClose()
        navigate({ to: '/', search: {} })
      },
      onError: (err) =>
        toast.error(err instanceof Error ? err.message : 'Kunde inte lämna hushållet'),
    })
  }

  return (
    <div className="flex flex-col gap-3">
      {preview.soleMember ? (
        <>
          <div className="rounded-2xl border border-border bg-accent p-3.5 text-sm leading-relaxed">
            Du är ensam kvar. När du lämnar <b>arkiveras hushållet</b> — det döljs men kan
            återställas så länge du vill.
          </div>
          <p className="text-sm text-muted-foreground">Inga öppna skulder att göra upp.</p>
        </>
      ) : (
        <>
          {preview.viewerOpenDebts.length > 0 && (
            <div className="rounded-2xl border border-destructive/20 bg-destructive/10 p-3.5">
              <p className="text-[12.5px] font-bold text-destructive">Du har öppna skulder här</p>
              <div className="mt-2 flex flex-col gap-1.5">
                {preview.viewerOpenDebts.map((p) => (
                  <div key={p.memberId} className="flex items-center justify-between text-[13px]">
                    <span>
                      {p.relation === 'youOwe'
                        ? `Du är skyldig ${p.name}`
                        : `${p.name} är skyldig dig`}
                    </span>
                    <Money
                      minor={p.netMinor}
                      intent={p.relation === 'youOwe' ? 'destructive' : 'success'}
                      className="text-[13px] font-semibold"
                    />
                  </div>
                ))}
              </div>
            </div>
          )}
          <p className="text-sm text-muted-foreground">
            Skulderna försvinner inte — gör upp först om ni vill. Historiken finns kvar för de
            andra, och du kan bjudas in igen.
          </p>
        </>
      )}

      <div className="mt-2 flex flex-col gap-2">
        <Button className={DANGER_BTN} onClick={onConfirm} disabled={leave.isPending}>
          {leave.isPending && <Loader2Icon className="animate-spin" />}
          {preview.soleMember ? 'Lämna och arkivera' : 'Lämna ändå'}
        </Button>
        <Button variant="outline" className="w-full" onClick={onClose} disabled={leave.isPending}>
          Avbryt
        </Button>
      </div>
    </div>
  )
}

export function LeaveHouseholdSheet({
  open,
  onClose,
  householdId,
}: {
  open: boolean
  onClose: () => void
  householdId?: string
}) {
  const preview = useRemovalPreview(open ? householdId : undefined)
  const household = useHousehold(open ? householdId : undefined)
  const name = household.data?.name ?? 'hushållet'

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title={`Lämna ${name}?`}
    >
      {preview.isLoading ? (
        <LoadingState rows={2} />
      ) : preview.isError ? (
        <ErrorState error={preview.error} onRetry={() => preview.refetch()} />
      ) : preview.data && householdId ? (
        <LeaveBody
          householdId={householdId}
          householdName={name}
          preview={preview.data}
          onClose={onClose}
        />
      ) : null}
    </ResponsiveSheet>
  )
}
