/**
 * "Lägg till en vän" — the add-a-friend affordance from the overview
 * (contacts-phone-sms spec §2.4, design frame 4). AFFORDANCE + COPY ONLY.
 *
 * The contacts model — add-by-number as a blind SMS invite, no lookup oracle,
 * contacts reusable across households — is a separate workstream
 * (docs/specs/contacts-phone-sms.md). This sheet deliberately
 * wires NO contact endpoints, query, or `Dina kontakter` data: it presents the
 * entry point and copy so the surface exists, and defers the real send + contact
 * graph to that spec's implementation.
 */
import { useState } from 'react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ResponsiveSheet } from '@/components/responsive-sheet'

const UPLABEL =
  'text-[11px] font-semibold uppercase tracking-[0.07em] text-muted-foreground'

export function AddFriendSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Lägg till en vän"
      description="Skicka en inbjudan via telefonnummer eller mejl. Vi avslöjar aldrig om numret redan finns på Settl."
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <span className={UPLABEL}>Telefonnummer</span>
          <div className="flex items-center gap-2">
            <span className="flex h-9 items-center rounded-md border border-input px-3 text-sm text-muted-foreground">
              🇸🇪 +46
            </span>
            <Input
              aria-label="Telefonnummer"
              inputMode="tel"
              placeholder="70 123 45 67"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              className="h-9"
            />
          </div>
        </div>

        <div className="flex items-center gap-3 text-[11.5px] text-muted-foreground">
          <span className="h-px flex-1 bg-border" />
          eller
          <span className="h-px flex-1 bg-border" />
        </div>

        <div className="flex flex-col gap-1.5">
          <span className={UPLABEL}>E-post</span>
          <Input
            aria-label="E-post"
            type="email"
            placeholder="namn@exempel.se"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="h-9"
          />
        </div>

        <Button
          type="button"
          className="w-full"
          onClick={() =>
            toast('Vän-inbjudningar via kontakter är på väg — kontaktmodellen byggs separat.')
          }
        >
          Skicka inbjudan
        </Button>

        <p className="text-[12.5px] text-muted-foreground">
          Kontakter sparas när en inbjudan accepteras och kan återanvändas i alla hushåll.
        </p>
      </div>
    </ResponsiveSheet>
  )
}
