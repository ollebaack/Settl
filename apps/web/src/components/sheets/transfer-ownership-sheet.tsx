/**
 * Överför ägarskap (ADR-0016 / docs/specs/household-ownership.md, design frame 4).
 * The owner picks another current member to become the new owner; the previous owner
 * becomes an ordinary member. Owner-only, never automatic (ADR-0006 guard is server-side).
 */
import { useEffect, useState } from 'react'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'

import { MemberAvatar } from '@/components/member-avatar'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useHousehold, useMe, useTransferOwnership } from '@/lib/queries'
import type { HouseholdDto, MeDto } from '@/lib/api'

function TransferBody({
  household,
  me,
  onClose,
}: {
  household: HouseholdDto
  me: MeDto
  onClose: () => void
}) {
  const transfer = useTransferOwnership(household.id)
  // Candidates = every current member except the acting owner.
  const candidates = household.members.filter((m) => m.id !== me.id)
  const [selected, setSelected] = useState<string | undefined>(candidates[0]?.id)

  const selectedName = candidates.find((c) => c.id === selected)?.name

  const onConfirm = () => {
    if (!selected || transfer.isPending) return
    transfer.mutate(
      { newOwnerMemberId: selected },
      {
        onSuccess: () => {
          toast(`${selectedName} är nu ägare av ${household.name}`)
          onClose()
        },
        onError: (err) =>
          toast.error(err instanceof Error ? err.message : 'Kunde inte överföra ägarskapet'),
      },
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-muted-foreground">
        Välj vem som blir ägare av {household.name}. Du blir vanlig medlem.
      </p>

      <div className="flex flex-col gap-2">
        {candidates.map((m) => {
          const active = m.id === selected
          return (
            <button
              key={m.id}
              type="button"
              onClick={() => setSelected(m.id)}
              className={cn(
                'flex items-center gap-3 rounded-2xl border p-3 text-left transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring',
                active ? 'border-primary bg-accent' : 'border-border bg-card hover:bg-muted/40',
              )}
            >
              <MemberAvatar name={m.name} avatarColor={m.avatarColor} size="sm" />
              <span className="flex-1 text-sm font-semibold">{m.name}</span>
              <span
                aria-hidden="true"
                className={cn(
                  'grid size-[18px] place-items-center rounded-full border text-[11px] font-bold',
                  active
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border text-transparent',
                )}
              >
                ✓
              </span>
            </button>
          )
        })}
      </div>

      <div className="flex flex-col gap-2">
        <Button
          className="w-full"
          onClick={onConfirm}
          disabled={!selected || transfer.isPending}
        >
          {transfer.isPending && <Loader2Icon className="animate-spin" />}
          {selectedName ? `Överför till ${selectedName}` : 'Överför'}
        </Button>
        <Button variant="outline" className="w-full" onClick={onClose} disabled={transfer.isPending}>
          Avbryt
        </Button>
      </div>
    </div>
  )
}

export function TransferOwnershipSheet({
  open,
  onClose,
  householdId,
}: {
  open: boolean
  onClose: () => void
  householdId?: string
}) {
  const household = useHousehold(open ? householdId : undefined)
  const me = useMe()

  // Reset the sheet's internal selection each time it (re)opens by remounting the body.
  const [mountKey, setMountKey] = useState(0)
  useEffect(() => {
    if (open) setMountKey((k) => k + 1)
  }, [open])

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Överför ägarskap"
    >
      {household.isLoading || me.isLoading ? (
        <LoadingState rows={2} />
      ) : household.isError ? (
        <ErrorState error={household.error} onRetry={() => household.refetch()} />
      ) : household.data && me.data ? (
        <TransferBody key={mountKey} household={household.data} me={me.data} onClose={onClose} />
      ) : null}
    </ResponsiveSheet>
  )
}
