/**
 * Arkivera hushåll (ADR-0016 / docs/specs/household-ownership.md, design frame 5).
 * Owner-only soft archive: hides the household for everyone, keeps all data, restorable.
 * Shows how many members are affected and the household-wide open-debt total; debts warn
 * but never block. Figures are DERIVED server-side via useRemovalPreview (ADR-0006).
 */
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'

import { Money } from '@/components/money'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { Button } from '@/components/ui/button'
import { useArchiveHousehold, useHousehold, useRemovalPreview } from '@/lib/queries'
import type { RemovalPreviewDto } from '@/lib/api'

const DANGER_BTN = 'w-full bg-destructive text-destructive-foreground hover:bg-destructive/90'

function ArchiveBody({
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
  const archive = useArchiveHousehold(householdId)
  const openTotal =
    typeof preview.householdOpenTotalMinor === 'number'
      ? preview.householdOpenTotalMinor
      : Number(preview.householdOpenTotalMinor)

  const onConfirm = () => {
    if (archive.isPending) return
    archive.mutate(undefined, {
      onSuccess: () => {
        toast(`${householdName} arkiverades`)
        onClose()
        navigate({ to: '/', search: {} })
      },
      onError: (err) =>
        toast.error(err instanceof Error ? err.message : 'Kunde inte arkivera hushållet'),
    })
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="rounded-2xl border border-destructive/20 bg-destructive/10 p-3.5">
        <p className="text-[12.5px] font-bold text-destructive">
          Det här påverkar alla {preview.memberCount} medlemmar
        </p>
        <p className="mt-1.5 text-[13px] leading-relaxed">
          Hushållet döljs för alla.{' '}
          {openTotal > 0 ? (
            <>
              Ni har <Money minor={openTotal} className="font-semibold" /> i öppna skulder kvar
              som inte görs upp.
            </>
          ) : (
            'Inga öppna skulder kvar.'
          )}
        </p>
      </div>
      <p className="text-sm text-muted-foreground">
        Ingen raderas och inget försvinner. Du kan återställa hushållet när som helst under{' '}
        <b>Arkiverade</b>.
      </p>

      <div className="mt-2 flex flex-col gap-2">
        <Button className={DANGER_BTN} onClick={onConfirm} disabled={archive.isPending}>
          {archive.isPending && <Loader2Icon className="animate-spin" />}
          Arkivera
        </Button>
        <Button variant="outline" className="w-full" onClick={onClose} disabled={archive.isPending}>
          Avbryt
        </Button>
      </div>
    </div>
  )
}

export function ArchiveHouseholdSheet({
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
      title={`Arkivera ${name}?`}
    >
      {preview.isLoading ? (
        <LoadingState rows={2} />
      ) : preview.isError ? (
        <ErrorState error={preview.error} onRetry={() => preview.refetch()} />
      ) : preview.data && householdId ? (
        <ArchiveBody
          householdId={householdId}
          householdName={name}
          preview={preview.data}
          onClose={onClose}
        />
      ) : null}
    </ResponsiveSheet>
  )
}
