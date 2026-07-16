/**
 * Lägg till kontakt (ADR-0019, contacts-addendum screens 2–3). Sends a BLIND invite: typing a
 * number sends a tokenized SMS invite and reveals nothing about whether it's already on Settl
 * (no lookup / no enumeration oracle — see the privacy note). SMS is the default channel; email
 * is the alternative. On success the sheet shows the "sent" confirmation (screen 3).
 *
 * Self-contained (local open state) rather than a global search-param sheet, since it's launched
 * from the /kontakter screen. Pass a household to also add the invitee to it on accept (screen 4).
 */
import { useEffect, useState } from 'react'
import { Loader2Icon, LockIcon } from 'lucide-react'
import { toast } from 'sonner'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { useSendContactInvite } from '@/lib/queries'
import type { InviteChannel } from '@/lib/api'
import { cn } from '@/lib/utils'

export function AddContactSheet({
  open,
  onClose,
  householdId,
  householdName,
}: {
  open: boolean
  onClose: () => void
  householdId?: string
  householdName?: string
}) {
  const sendInvite = useSendContactInvite()
  const [channel, setChannel] = useState<InviteChannel>('sms')
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')
  const [sentTo, setSentTo] = useState<string | null>(null)

  // Reset whenever the sheet is (re)opened so a previous "sent" state never lingers.
  useEffect(() => {
    if (open) {
      setChannel('sms')
      setPhone('')
      setEmail('')
      setSentTo(null)
    }
  }, [open])

  const title = householdName ? `Lägg till i ${householdName}` : 'Lägg till kontakt'

  async function onSend() {
    const value = channel === 'sms' ? phone.trim() : email.trim()
    if (!value) {
      toast(channel === 'sms' ? 'Skriv ett telefonnummer' : 'Skriv en e-postadress')
      return
    }
    try {
      await sendInvite.mutateAsync({
        channel,
        phone: channel === 'sms' ? value : null,
        email: channel === 'email' ? value : null,
        householdId: householdId ?? null,
      })
      setSentTo(value)
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
      title={sentTo ? 'Inbjudan skickad' : title}
      description={
        sentTo
          ? undefined
          : 'Skicka en inbjudan. Tackar de ja blir de en sparad kontakt du kan återanvända i alla hushåll.'
      }
    >
      {sentTo ? (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col items-center gap-3 pt-2 text-center">
            <span aria-hidden="true" className="grid size-12 place-items-center rounded-full bg-accent text-2xl">
              📨
            </span>
            <p className="max-w-[290px] text-sm text-muted-foreground text-balance">
              Vi har skickat en inbjudan till{' '}
              <span className="font-mono font-semibold text-foreground">{sentTo}</span>. När de
              tackar ja dyker de upp som en kontakt här. Länken går ut om 7 dagar.
            </p>
          </div>
          <div className="rounded-2xl border border-border bg-accent/40 p-3 text-xs leading-relaxed text-muted-foreground">
            Tills de svarar visas de under <span className="font-semibold">Väntar på svar</span>.
            Du kan skicka igen därifrån om det behövs.
          </div>
          <Button type="button" onClick={onClose} className="w-full">
            Klart
          </Button>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          <ToggleGroup
            value={[channel]}
            onValueChange={(v) => setChannel((v[0] as InviteChannel | undefined) ?? channel)}
            className="w-full rounded-xl bg-muted p-1"
          >
            <ToggleGroupItem value="sms" className="flex-1 rounded-lg text-sm">
              Telefonnummer
            </ToggleGroupItem>
            <ToggleGroupItem value="email" className="flex-1 rounded-lg text-sm">
              E-post
            </ToggleGroupItem>
          </ToggleGroup>

          {channel === 'sms' ? (
            <div className="flex items-center gap-2 rounded-xl border border-border bg-card px-3">
              <span className="text-sm font-semibold text-muted-foreground">+46</span>
              <span aria-hidden="true" className="h-5 w-px bg-border" />
              <Input
                aria-label="Telefonnummer"
                inputMode="tel"
                autoComplete="tel"
                placeholder="70 123 45 67"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                className="border-0 bg-transparent px-0 font-mono focus-visible:ring-0"
              />
            </div>
          ) : (
            <Input
              aria-label="E-post"
              type="email"
              autoComplete="email"
              placeholder="namn@exempel.se"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          )}

          <div className="flex items-start gap-2 rounded-xl bg-muted p-3 text-xs leading-relaxed text-muted-foreground">
            <LockIcon className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
            <span>
              Vi berättar inte om numret redan använder Settl — vi skickar bara inbjudan dit. Det
              skyddar andras integritet.
            </span>
          </div>

          <div className="mt-2 flex flex-col gap-2">
            <Button
              type="button"
              onClick={onSend}
              disabled={sendInvite.isPending}
              className={cn('w-full', !(channel === 'sms' ? phone.trim() : email.trim()) && 'opacity-45')}
            >
              {sendInvite.isPending && <Loader2Icon className="animate-spin" />}
              {channel === 'sms' ? 'Skicka SMS-inbjudan' : 'Skicka inbjudan'}
            </Button>
            <Button type="button" variant="outline" onClick={onClose} className="w-full">
              Avbryt
            </Button>
          </div>
        </div>
      )}
    </ResponsiveSheet>
  )
}
