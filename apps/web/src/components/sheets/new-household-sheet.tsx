/**
 * Nytt hushåll — create a household with just a name. The acting user is its sole
 * initial member; everyone else joins via invite (ADR-0005, sent from household
 * settings after creation), not typed in here.
 */
import { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'

import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useActiveHousehold } from '@/lib/active-household'
import { useCreateHousehold } from '@/lib/queries'
import { cn } from '@/lib/utils'

export function NewHouseholdSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { households, setHouseholdId } = useActiveHousehold()
  const createHousehold = useCreateHousehold()

  const [name, setName] = useState('')

  async function onSave() {
    const trimmedName = name.trim()
    if (!trimmedName) {
      toast('Ge hushållet ett namn först')
      return
    }

    try {
      const created = await createHousehold.mutateAsync({ name: trimmedName, currency: null })
      setHouseholdId(created.id)
      // Enter the fresh book. When it's the user's first household, `/` IS its
      // dashboard (adaptive home, contacts-phone-sms spec); with others already present, `/`
      // would be the overview, so drop straight into the focused book route.
      if (households.length === 0) {
        navigate({ to: '/', search: {} })
      } else {
        navigate({ to: '/hushall/$id', params: { id: created.id }, search: {} })
      }
      toast(`${created.name} skapat — bjud in andra från hushållets inställningar`)
      setName('')
      onClose()
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Nytt hushåll"
      description="En egen bok för stället eller gänget — bjud in andra efteråt."
    >
      <div className="flex flex-col gap-5">
        <div className="flex flex-col gap-1.5">
          <Input
            aria-label="Namn"
            placeholder="Sommarstugan, Kollektivet, Resgänget…"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>

        <Button
          type="button"
          onClick={onSave}
          disabled={createHousehold.isPending}
          className={cn('w-full', !name.trim() && 'opacity-45')}
        >
          {createHousehold.isPending && <Loader2Icon className="animate-spin" />}
          Skapa hushåll
        </Button>
      </div>
    </ResponsiveSheet>
  )
}
