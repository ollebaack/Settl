/**
 * Profil `/profil` — change your name, pick an avatar emoji, and choose your nudge tone
 * (profile-addendum §2.1, frames 1–2; ADR-0019; implementation-map §2.4, ambiguity #18).
 * Part of the app chrome (AppShell). Renders and calls only (ADR-0006): the avatar is the
 * email-derived `avatarColor` with an optional emoji over it, letter initial as the fallback.
 * Name + emoji + tone persist via PUT /me.
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
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { MemberAvatar } from '@/components/member-avatar'
import { AvatarPickerSheet } from '@/components/sheets/avatar-picker-sheet'
import { useLogout, useMe, useUpdateMe } from '@/lib/queries'
import type { MeDto, NudgeTone } from '@/lib/api'
import { cn } from '@/lib/utils'

export const Route = createFileRoute('/profil')({
  component: () => (
    <RequireAuth>
      <ProfilePage />
    </RequireAuth>
  ),
})

const UPLABEL = 'text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground'

// The nudge tone only selects copy, never which nudges fire (implementation-map §2.4). The
// label + one-liner describe each voice; the API is authoritative over the actual nudge text.
const TONE_COPY: Record<NudgeTone, { label: string; hint: string }> = {
  direct: { label: 'Rak', hint: 'Tydliga knuffar med belopp och en uppmaning att göra upp.' },
  gentle: { label: 'Mjuk', hint: 'Vänliga påminnelser utan press.' },
}

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
  // The API only ever returns 'gentle' | 'direct'; fall back to the default for safety.
  const [tone, setTone] = useState<NudgeTone>((me.nudgeTone as NudgeTone) ?? 'direct')
  // Daily nudge-digest email opt-in (reminder-delivery spec). On by default.
  const [emailsEnabled, setEmailsEnabled] = useState<boolean>(me.nudgeEmailsEnabled ?? true)

  const emoji = me.avatarEmoji ?? null

  /** Persist name + the given emoji + the current tone + email opt-in. Returns true on success. */
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
        nudgeTone: tone,
        nudgeEmailsEnabled: emailsEnabled,
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

      {/* Nudge tone */}
      <div className="flex flex-col gap-1.5">
        <p className={UPLABEL}>Knuffar</p>
        <ToggleGroup
          value={[tone]}
          onValueChange={(v) => setTone((v[0] as NudgeTone | undefined) ?? tone)}
          className="w-full rounded-xl bg-muted p-1"
        >
          {(['direct', 'gentle'] as const).map((t) => (
            <ToggleGroupItem key={t} value={t} className="flex-1 rounded-lg text-sm">
              {TONE_COPY[t].label}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>
        <p className="text-xs text-muted-foreground">{TONE_COPY[tone].hint}</p>

        {/* Nudge-email opt-in (reminder-delivery spec). Separate from tone: tone styles in-app
            nudges, this controls the daily digest email. Off is honoured server-side. */}
        <div className="mt-3 flex flex-col gap-1.5">
          <p className={UPLABEL}>Påminnelser via e-post</p>
          <ToggleGroup
            value={[emailsEnabled ? 'on' : 'off']}
            onValueChange={(v) => setEmailsEnabled((v[0] ?? (emailsEnabled ? 'on' : 'off')) === 'on')}
            className="w-full rounded-xl bg-muted p-1"
          >
            <ToggleGroupItem value="on" className="flex-1 rounded-lg text-sm">
              På
            </ToggleGroupItem>
            <ToggleGroupItem value="off" className="flex-1 rounded-lg text-sm">
              Av
            </ToggleGroupItem>
          </ToggleGroup>
          <p className="text-xs text-muted-foreground">
            {emailsEnabled
              ? 'Ett dagligt mejl när du har knuffar att ta tag i. Aldrig mer än ett per dag.'
              : 'Du får inga påminnelser via e-post. Knuffar visas fortfarande i appen.'}
          </p>
        </div>
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
