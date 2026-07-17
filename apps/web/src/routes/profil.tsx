/**
 * Profil `/profil` — change your name, pick an avatar emoji, and opt into e-post reminders
 * (profile-addendum §2.1, frames 1–2; ADR-0019; implementation-map §2.4, ambiguity #18).
 * Part of the app chrome (AppShell). Renders and calls only (ADR-0006): the avatar is the
 * email-derived `avatarColor` with an optional emoji over it, letter initial as the fallback.
 * Name + emoji + email opt-in persist via PUT /me. Nudge tone is fixed to the direct voice.
 *
 * Scope note: the design's Konto card also shows a "Byt lösenord" row (§2.3). Password
 * change is out of this feature's scope (no endpoint yet), so that row is intentionally
 * omitted rather than shipped as a dead link — the read-only email row stays.
 */
import { useState } from 'react'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { ChevronLeftIcon, Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { RequireAuth } from '@/components/require-auth'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { MemberAvatar } from '@/components/member-avatar'
import { AvatarPickerSheet } from '@/components/sheets/avatar-picker-sheet'
import { useLogout, useMe, useUpdateMe } from '@/lib/queries'
import type { MeDto } from '@/lib/api'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/profil')({
  component: () => (
    <RequireAuth>
      <ProfilePage />
    </RequireAuth>
  ),
})

const UPLABEL = 'text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground'

function ProfilePage() {
  const { data: me } = useMe()
  // RequireAuth guarantees an authenticated, confirmed member before this renders.
  if (!me) return null
  // Re-init the form state whenever the underlying member identity changes.
  return <ProfileForm key={me.id} me={me} />
}

function ProfileForm({ me }: { me: MeDto }) {
  const navigate = useNavigate()
  const updateMe = useUpdateMe()
  const logout = useLogout()

  const [name, setName] = useState(me.name)
  const [nameError, setNameError] = useState('')
  const [sheetOpen, setSheetOpen] = useState(false)
  // Optional Swish number (swish-settlement-payments spec). Prefill from the stored E.164,
  // showing the Swedish subscriber part after the +46 chip — same convention as the profile phone.
  const [swish, setSwish] = useState(
    me.swishNumber?.startsWith('+46') ? me.swishNumber.slice(3) : (me.swishNumber ?? ''),
  )
  // Daily nudge-digest email opt-in (reminder-delivery spec). Off by default — an explicit opt-in.
  const [emailsEnabled, setEmailsEnabled] = useState<boolean>(me.nudgeEmailsEnabled ?? false)

  const emoji = me.avatarEmoji ?? null

  /** Persist name + the given emoji + the (fixed direct) tone + email opt-in. Returns true on success. */
  async function save(nextEmoji: string | null, successMsg: string): Promise<boolean> {
    const trimmed = name.trim()
    if (!trimmed) {
      setNameError('Ange ditt namn.')
      return false
    }
    setNameError('')
    try {
      await updateMe.mutateAsync({
        name: trimmed,
        avatarEmoji: nextEmoji,
        // Nudge tone is fixed to the direct voice — no user-facing choice anymore.
        nudgeTone: 'direct',
        nudgeEmailsEnabled: emailsEnabled,
        // The form always submits the current value; empty clears it server-side (API validates
        // and stores E.164). Sent on every save so a name/emoji edit never wipes the number.
        swishNumber: swish.trim() || null,
      })
      toast(successMsg)
      return true
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
      return false
    }
  }

  async function onLogout() {
    try {
      await logout.mutateAsync()
      navigate({ to: '/login' })
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="gap-1 px-2 text-muted-foreground"
          onClick={() => navigate({ to: '/' })}
        >
          <ChevronLeftIcon className="size-4" />
          Tillbaka
        </Button>
        <span className={UPLABEL}>Profil</span>
        <span className="w-[72px]" aria-hidden="true" />
      </div>

      {/* Avatar block */}
      <div className="flex flex-col items-center gap-3 pt-1">
        <MemberAvatar
          name={name}
          avatarColor={me.avatarColor}
          avatarEmoji={emoji}
          size="xl"
          className="ring-4 ring-background"
        />
        {emoji ? (
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              size="sm"
              className="rounded-full"
              onClick={() => setSheetOpen(true)}
            >
              Byt emoji
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="rounded-full text-muted-foreground"
              disabled={updateMe.isPending}
              onClick={() => void save(null, 'Profilbild uppdaterad')}
            >
              Ta bort
            </Button>
          </div>
        ) : (
          <Button
            type="button"
            variant="secondary"
            size="sm"
            className="rounded-full text-primary"
            onClick={() => setSheetOpen(true)}
          >
            Välj profilbild
          </Button>
        )}
      </div>

      {/* Name */}
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="profile-name" className={UPLABEL}>
          Namn
        </Label>
        <Input
          id="profile-name"
          value={name}
          aria-invalid={!!nameError}
          onChange={(e) => {
            setName(e.target.value)
            if (nameError) setNameError('')
          }}
        />
        {nameError ? (
          <p className="text-xs text-destructive">{nameError}</p>
        ) : (
          <p className="text-xs text-muted-foreground">Så här syns du i loggboken.</p>
        )}
      </div>

      {/* Swish number (swish-settlement-payments spec) — optional, unverified contact data
          (tech-debt/0010), stored separately from any profile phone. Powers the debtor's "Betala
          med Swish" action on the settle-up sheet. The +46 chip is cosmetic; the API validates. */}
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="profile-swish" className={UPLABEL}>
          Swish-nummer{' '}
          <span className="font-normal normal-case tracking-normal">(valfritt)</span>
        </Label>
        <div className="flex items-center gap-2 rounded-xl border border-border bg-card px-3">
          <span className="text-sm font-semibold text-muted-foreground">+46</span>
          <span aria-hidden="true" className="h-5 w-px bg-border" />
          <Input
            id="profile-swish"
            aria-label="Ditt Swish-nummer"
            inputMode="tel"
            autoComplete="tel"
            placeholder="73 555 12 34"
            value={swish}
            onChange={(e) => setSwish(e.target.value)}
            className="border-0 bg-transparent px-0 font-mono focus-visible:ring-0"
          />
        </div>
        <p className="text-xs text-muted-foreground">
          Låter andra i hushållet betala dig med Swish när ni gör upp. Sparas som det är —
          inte verifierat.
        </p>
      </div>

      {/* Account */}
      <div className="flex flex-col gap-1.5">
        <p className={UPLABEL}>Konto</p>
        <Card className="gap-0 px-4 py-0">
          <div className="flex items-center gap-3 py-3">
            <div className="flex min-w-0 flex-1 flex-col">
              <span className="text-[13.5px] font-semibold">E-post</span>
              <span className="truncate text-xs text-muted-foreground">{me.email ?? '—'}</span>
            </div>
            <span className="shrink-0 text-[11.5px] text-muted-foreground">Din inloggning</span>
          </div>
        </Card>
      </div>

      {/* Nudge-email opt-in (reminder-delivery spec) — the daily digest email. Off by default;
          off is honoured server-side. In-app nudges always use the direct voice. */}
      <div className="flex flex-col gap-1.5">
        <p className={UPLABEL}>Påminnelser via e-post</p>
        <Label
          htmlFor="nudge-emails"
          className="flex items-center gap-3 rounded-xl border border-border bg-card px-4 py-3"
        >
          <span className="min-w-0 flex-1 text-[13.5px] font-normal">
            {emailsEnabled
              ? 'Ett dagligt mejl när du har knuffar att ta tag i. Aldrig mer än ett per dag.'
              : 'Du får inga påminnelser via e-post. Knuffar visas fortfarande i appen.'}
          </span>
          <Switch
            id="nudge-emails"
            aria-label="Påminnelser via e-post"
            checked={emailsEnabled}
            onCheckedChange={setEmailsEnabled}
          />
        </Label>
      </div>

      {/* Actions */}
      <div className="flex flex-col gap-2">
        <Button
          type="button"
          disabled={updateMe.isPending}
          onClick={() => void save(emoji, 'Profilen sparad')}
        >
          {updateMe.isPending && <Loader2Icon className="animate-spin" />}
          {updateMe.isPending ? 'Sparar…' : 'Spara'}
        </Button>
        <Button
          type="button"
          variant="outline"
          className={cn('border-destructive text-destructive hover:bg-destructive/10')}
          disabled={logout.isPending}
          onClick={onLogout}
        >
          Logga ut
        </Button>
      </div>

      <AvatarPickerSheet
        open={sheetOpen}
        onClose={() => setSheetOpen(false)}
        name={name}
        avatarColor={me.avatarColor}
        currentEmoji={emoji}
        busy={updateMe.isPending}
        onCommit={(picked) => {
          void save(picked, 'Profilbild uppdaterad').then((ok) => {
            if (ok) setSheetOpen(false)
          })
        }}
      />
    </div>
  )
}
