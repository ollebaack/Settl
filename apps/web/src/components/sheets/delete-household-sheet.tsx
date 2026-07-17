/**
 * Ta bort hushåll (ADR-0022 / docs/specs/household-ownership.md). Owner-only, terminal,
 * and only for an EMPTY household (no entries, recurring templates, or settlements) — a
 * carve-out to ADR-0016's soft-only archive rule, for cleaning up mistakes. Extra members
 * don't block: they're warned, then lose access. Emptiness is DERIVED server-side via
 * useRemovalPreview.isEmpty (ADR-0006); a non-empty household is directed to archive.
 */
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'

import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { Button } from '@/components/ui/button'
import { useDeleteHousehold, useHousehold, useRemovalPreview } from '@/lib/queries'
import type { RemovalPreviewDto } from '@/lib/api'

const DANGER_BTN = 'w-full bg-destructive text-destructive-foreground hover:bg-destructive/90'

function DeleteBody({
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
  const del = useDeleteHousehold(householdId)
  const otherMembers = Math.max(0, Number(preview.memberCount) - 1)

  // A non-empty household can't be hard-deleted — steer to archive instead.
  if (!preview.isEmpty) {
    return (
      <div className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">
          Hushållet har poster och kan inte tas bort. Arkivera det i stället — då döljs det
          men allt sparas.
        </p>
        <div className="mt-2 flex flex-col gap-2">
          <Button
            variant="outline"
            className="w-full"
            onClick={() => navigate({ to: '.', search: (p) => ({ ...p, sheet: 'archiveHousehold' }) })}
          >
            Arkivera i stället
          </Button>
          <Button variant="outline" className="w-full" onClick={onClose}>
            Avbryt
          </Button>
        </div>
      </div>
    )
  }

  const onConfirm = () => {
    if (del.isPending) return
    del.mutate(undefined, {
      onSuccess: () => {
        toast(`${householdName} togs bort`)
        onClose()
        navigate({ to: '/', search: {} })
      },
      onError: (err) =>
        toast.error(err instanceof Error ? err.message : 'Kunde inte ta bort hushållet'),
    })
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="rounded-2xl border border-destructive/20 bg-destructive/10 p-3.5">
        <p className="text-[12.5px] font-bold text-destructive">
          Det här går inte att ångra
        </p>
        <p className="mt-1.5 text-[13px] leading-relaxed">
          Hushållet tas bort permanent.{' '}
          {otherMembers > 0
            ? `${otherMembers} ${otherMembers === 1 ? 'annan medlem' : 'andra medlemmar'} förlorar åtkomst.`
            : 'Bara du är medlem.'}
        </p>
      </div>
      <p className="text-sm text-muted-foreground">
        Eftersom hushållet är tomt finns inget att spara. Vill du behålla det kan du arkivera
        i stället.
      </p>

      <div className="mt-2 flex flex-col gap-2">
        <Button className={DANGER_BTN} onClick={onConfirm} disabled={del.isPending}>
          {del.isPending && <Loader2Icon className="animate-spin" />}
          Ta bort permanent
        </Button>
        <Button variant="outline" className="w-full" onClick={onClose} disabled={del.isPending}>
          Avbryt
        </Button>
      </div>
    </div>
  )
}

export function DeleteHouseholdSheet({
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
      title={`Ta bort ${name}?`}
    >
      {preview.isLoading ? (
        <LoadingState rows={2} />
      ) : preview.isError ? (
        <ErrorState error={preview.error} onRetry={() => preview.refetch()} />
      ) : preview.data && householdId ? (
        <DeleteBody
          householdId={householdId}
          householdName={name}
          preview={preview.data}
          onClose={onClose}
        />
      ) : null}
    </ResponsiveSheet>
  )
}
