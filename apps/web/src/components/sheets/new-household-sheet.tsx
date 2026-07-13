/**
 * Nytt hushåll — create a household with a name and, optionally, members typed
 * in by name (implementation-map §2.5b). The acting user is always a member
 * server-side (never listed); typed names become brand-new Members with no
 * invite step — auth is deferred (ADR-0005) so a Member is just a name.
 */
import { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon, XIcon } from 'lucide-react'
import { toast } from 'sonner'

import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { MemberAvatar } from '@/components/member-avatar'
import { useActiveHousehold } from '@/lib/active-household'
import { useMe, useCreateHousehold } from '@/lib/queries'
import { cn } from '@/lib/utils'

export function NewHouseholdSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { setHouseholdId } = useActiveHousehold()
  const { data: me } = useMe()
  const createHousehold = useCreateHousehold()

  const [name, setName] = useState('')
  const [members, setMembers] = useState<string[]>([''])

  function resetForm() {
    setName('')
    setMembers([''])
  }

  function updateMember(i: number, value: string) {
    setMembers((prev) => prev.map((m, idx) => (idx === i ? value : m)))
  }

  function removeMember(i: number) {
    setMembers((prev) => prev.filter((_, idx) => idx !== i))
  }

  async function onSave() {
    const trimmedName = name.trim()
    if (!trimmedName) {
      toast('Ge hushållet ett namn först')
      return
    }

    try {
      const created = await createHousehold.mutateAsync({
        name: trimmedName,
        currency: null,
        memberIds: null,
        newMemberNames: members.map((m) => m.trim()).filter(Boolean),
      })
      setHouseholdId(created.id)
      navigate({ to: '/', search: {} })
      toast(`${created.name} skapat — tomt blad`)
      resetForm()
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
      description="En egen bok för stället eller gänget — bjud in vilka som helst."
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

        <Card size="sm" className="gap-0 py-2">
          <div className="flex items-center gap-3 border-b border-border px-4 py-2.5">
            {me && <MemberAvatar name={me.name} avatarColor={me.avatarColor} size="sm" />}
            <span className="flex-1 text-sm font-medium">Du</span>
            <span className="text-xs text-muted-foreground">alltid med</span>
          </div>

          {members.map((value, i) => (
            <div key={i} className="flex items-center gap-2 border-b border-border px-4 py-2">
              <Input
                aria-label="Medlemsnamn"
                placeholder="Namn"
                value={value}
                onChange={(e) => updateMember(i, e.target.value)}
                className="h-8"
              />
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                onClick={() => removeMember(i)}
                aria-label="Ta bort medlem"
              >
                <XIcon />
              </Button>
            </div>
          ))}

          <Button
            type="button"
            variant="ghost"
            className="w-full text-primary"
            onClick={() => setMembers((prev) => [...prev, ''])}
          >
            + Lägg till medlem
          </Button>
        </Card>

        <p className="text-xs text-muted-foreground">
          I riktiga appen får medlemmarna en inbjudan — här läggs de till direkt.
        </p>

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
