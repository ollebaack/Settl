/**
 * Kontakter (/kontakter) — ADR-0019, contacts-addendum screens 1, 5 & 6.
 *
 * A saved contact is the Member↔Member edge that only exists once an invite is accepted
 * (connection-on-accept). Outstanding invites the user sent sit under "Väntar på svar" as the
 * raw number/email, not a person. "+ Lägg till kontakt" sends a BLIND invite (AddContactSheet) —
 * there is deliberately no user search anywhere. The profile phone at the bottom is optional and
 * stored UNVERIFIED (no OTP — tech-debt/0010); email stays the login.
 *
 * The profile phone lives here (rather than a not-yet-built /profil page) because it is the same
 * feature slice — a member's own contact number — and the addendum ties it to the contacts flow.
 */
import { useEffect, useState } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { RequireAuth } from '@/components/require-auth'
import { AddContactSheet } from '@/components/sheets/add-contact-sheet'
import { MemberAvatar } from '@/components/member-avatar'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState, ErrorState } from '@/components/screen-states'
import { shortDate } from '@/lib/format'
import { useContacts, useMe, usePendingContactInvites, useUpdateProfile } from '@/lib/queries'
import type { ContactDto, PendingInviteDto } from '@/lib/api'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/kontakter')({
  component: () => (
    <RequireAuth>
      <ContactsPage />
    </RequireAuth>
  ),
})

const SECTION_LABEL =
  'text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground'

function sharedLabel(rawCount: number | string): string {
  const count = Number(rawCount)
  if (count === 0) return 'Ingen delad bok än'
  if (count === 1) return 'I 1 hushåll med dig'
  return `I ${count} hushåll med dig`
}

function ContactsPage() {
  const contacts = useContacts()
  const pending = usePendingContactInvites()
  const [addOpen, setAddOpen] = useState(false)

  const list = contacts.data ?? []
  const pendingList = pending.data ?? []
  const isEmpty = list.length === 0 && pendingList.length === 0

  return (
    <div className="space-y-4">
      <div>
        <h1 className="font-heading text-lg font-bold tracking-tight">Kontakter</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Sparade vänner och familj — lägg till via telefonnummer.
        </p>
      </div>

      {contacts.isPending ? (
        <div className="flex flex-col gap-2" aria-busy="true">
          <Skeleton className="h-16 w-full rounded-2xl" />
          <Skeleton className="h-16 w-full rounded-2xl" />
        </div>
      ) : contacts.isError ? (
        <ErrorState error={contacts.error} onRetry={() => void contacts.refetch()} />
      ) : isEmpty ? (
        <EmptyState icon={<span className="text-3xl">👋</span>} className="py-12">
          Inga kontakter än. Lägg till vänner och familj via telefonnummer, så slipper du skriva in
          dem varje gång du delar ett hushåll.
        </EmptyState>
      ) : (
        <>
          {list.length > 0 && (
            <Card size="sm" className="divide-y divide-border p-0">
              {list.map((c) => (
                <ContactRow key={c.memberId} contact={c} />
              ))}
            </Card>
          )}

          {pendingList.length > 0 && (
            <div className="space-y-2">
              <p className={SECTION_LABEL}>Väntar på svar</p>
              <Card size="sm" className="divide-y divide-border p-0">
                {pendingList.map((p) => (
                  <PendingRow key={p.id} invite={p} />
                ))}
              </Card>
            </div>
          )}
        </>
      )}

      <Button variant="outline" onClick={() => setAddOpen(true)} className="w-full border-dashed">
        + Lägg till kontakt
      </Button>

      <ProfilePhoneCard />

      <AddContactSheet open={addOpen} onClose={() => setAddOpen(false)} />
    </div>
  )
}

function ContactRow({ contact }: { contact: ContactDto }) {
  return (
    <div className="flex items-center gap-3 px-3.5 py-3">
      <MemberAvatar name={contact.name} avatarColor={contact.avatarColor} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold">{contact.name}</p>
        <p className="truncate text-xs text-muted-foreground">
          {sharedLabel(contact.sharedHouseholdCount)}
        </p>
      </div>
    </div>
  )
}

function PendingRow({ invite }: { invite: PendingInviteDto }) {
  const target = invite.channel === 'sms' ? invite.phone : invite.email
  const sentVia = invite.channel === 'sms' ? 'SMS-inbjudan' : 'E-postinbjudan'
  return (
    <div className="flex items-center gap-3 px-3.5 py-3">
      <span
        aria-hidden="true"
        className="grid size-9 shrink-0 place-items-center rounded-full bg-muted text-sm font-bold text-muted-foreground"
      >
        ?
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-mono text-[13.5px] font-semibold">{target}</p>
        <p className="truncate text-xs text-muted-foreground">
          {sentVia} skickad {shortDate(invite.sentAt)}
        </p>
      </div>
      <span className="rounded-full bg-muted px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
        Väntar
      </span>
    </div>
  )
}

/** Screen 5: the member's own optional, unverified profile phone (ADR-0019 / tech-debt/0010). */
function ProfilePhoneCard() {
  const { data: me } = useMe()
  const updateProfile = useUpdateProfile()
  const [local, setLocal] = useState('')

  // Prefill from the stored E.164, showing the Swedish subscriber part after the +46 chip.
  useEffect(() => {
    if (!me) return
    setLocal(me.phone?.startsWith('+46') ? me.phone.slice(3) : (me.phone ?? ''))
  }, [me])

  if (!me) return null

  async function onSave() {
    try {
      await updateProfile.mutateAsync({ phone: local.trim() || null })
      toast('Nummer sparat')
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <div className="space-y-2 border-t border-border pt-4">
      <p className={SECTION_LABEL}>
        Ditt nummer <span className="font-normal normal-case tracking-normal">(valfritt)</span>
      </p>
      <div className="flex items-center gap-2 rounded-xl border border-border bg-card px-3">
        <span className="text-sm font-semibold text-muted-foreground">+46</span>
        <span aria-hidden="true" className="h-5 w-px bg-border" />
        <Input
          aria-label="Ditt telefonnummer"
          inputMode="tel"
          autoComplete="tel"
          placeholder="73 555 12 34"
          value={local}
          onChange={(e) => setLocal(e.target.value)}
          className="border-0 bg-transparent px-0 font-mono focus-visible:ring-0"
        />
      </div>
      <div className="flex items-center gap-2">
        <span className="rounded-full bg-muted px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
          Overifierat
        </span>
        <span className="text-[11.5px] text-muted-foreground">
          Visas för dina kontakter. E-post är fortfarande din inloggning.
        </span>
      </div>
      <Button
        type="button"
        onClick={onSave}
        disabled={updateProfile.isPending}
        className={cn('w-full')}
      >
        {updateProfile.isPending && <Loader2Icon className="animate-spin" />}
        Spara
      </Button>
    </div>
  )
}
