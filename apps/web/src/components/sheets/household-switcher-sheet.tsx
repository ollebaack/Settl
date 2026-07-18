/**
 * Household switcher overlay (implementation-map §2.5). Lists every household
 * the acting user belongs to (many-to-many) with its net position, highlights
 * the active book, and switches on tap. Net + label are DERIVED server-side
 * (HouseholdListItemDto) — the sheet never computes balances (ADR-0006).
 */
import { useState } from 'react'
import { toast } from 'sonner'
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Money } from '@/components/money'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { HouseholdBadge } from '@/components/household-badge'
import { ErrorState, LoadingState } from '@/components/screen-states'
import { cn } from '@/lib/utils'
import { MemberAvatar } from '@/components/member-avatar'
import { shortDate } from '@/lib/format'
import { useActiveHousehold } from '@/lib/active-household'
import {
  useHouseholdInvites,
  useHouseholdsWithArchived,
  useInvitableContacts,
  useInviteContactToHousehold,
  useRestoreHousehold,
  useSendInvite,
} from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import type { MoneyIntent } from '@/components/money'
import type { HouseholdListItemDto, InvitableContactDto } from '@/lib/api'

/** household-list netLabel is Labels.Net: owed | owe | square. */
function netSub(label: string): string {
  if (label === 'owed') return 'du ska få'
  if (label === 'owe') return 'du är skyldig'
  return 'kvitt'
}

function netIntent(label: string): MoneyIntent {
  if (label === 'owed') return 'success'
  if (label === 'owe') return 'destructive'
  return 'muted'
}

export function HouseholdSwitcherSheet({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
  const navigate = useNavigate()
  const { householdId, setHouseholdId } = useActiveHousehold()
  const { openSheet } = useSheet()
  const query = useHouseholdsWithArchived()
  const all = query.data ?? []
  const households = all.filter((h) => !h.archivedAt)
  const archived = all.filter((h) => h.archivedAt)

  const select = (h: HouseholdListItemDto) => {
    setHouseholdId(h.id)
    // Enter the chosen book with a clean overlay/search state. With 2+ books `/`
    // is the overview (contacts-phone-sms spec), so drill into the focused book route; with a
    // single book `/` already IS its dashboard.
    if (households.length > 1) {
      navigate({ to: '/hushall/$id', params: { id: h.id }, search: {} })
    } else {
      navigate({ to: '/', search: {} })
    }
    toast(`Bytte till ${h.name}`)
  }

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Dina hushåll"
      description="En bok per hushåll — du kan vara med i flera."
    >
      {query.isLoading ? (
        <LoadingState rows={3} />
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2">
            {households.map((h) => {
              const active = h.id === householdId
              return (
                <Card
                  key={h.id}
                  role="button"
                  tabIndex={0}
                  onClick={() => select(h)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault()
                      select(h)
                    }
                  }}
                  size="sm"
                  className={cn(
                    'flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring',
                    active && 'ring-2 ring-primary',
                  )}
                >
                  <HouseholdBadge name={h.name} size="md" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{h.name}</p>
                    <p className="truncate text-xs text-muted-foreground">
                      {h.memberNames.join(', ')}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-col items-end">
                    <Money minor={h.netMinor} intent={netIntent(h.netLabel)} className="text-[13.5px]" />
                    <span className="text-xs text-muted-foreground">{netSub(h.netLabel)}</span>
                  </div>
                </Card>
              )
            })}
          </div>

          <Button
            variant="outline"
            onClick={() => openSheet('newHousehold')}
            className="w-full border-dashed"
          >
            + Nytt hushåll
          </Button>

          {archived.length > 0 && <ArchivedSection households={archived} />}

          <InviteSection
            householdId={householdId}
            householdName={households.find((h) => h.id === householdId)?.name}
          />
        </div>
      )}
    </ResponsiveSheet>
  )
}

/**
 * "Arkiverade" — soft-archived households (household-ownership spec, design frame 7). Shown greyed and
 * non-switchable; only the owner sees the "Återställ" button.
 */
function ArchivedSection({ households }: { households: HouseholdListItemDto[] }) {
  return (
    <div className="flex flex-col gap-2">
      <p className="text-xs font-semibold uppercase tracking-[0.09em] text-muted-foreground">
        Arkiverade
      </p>
      {households.map((h) => (
        <ArchivedRow key={h.id} household={h} />
      ))}
      <p className="text-xs text-muted-foreground">Bara ägaren ser återställ-knappen.</p>
    </div>
  )
}

function ArchivedRow({ household: h }: { household: HouseholdListItemDto }) {
  const restore = useRestoreHousehold(h.id)
  const onRestore = () => {
    if (restore.isPending) return
    restore.mutate(undefined, {
      onSuccess: () => toast(`${h.name} återställd`),
      onError: (err) =>
        toast.error(err instanceof Error ? err.message : 'Kunde inte återställa hushållet'),
    })
  }
  return (
    <Card
      size="sm"
      className="flex flex-row items-center gap-3 p-3 opacity-60"
    >
      <span
        aria-hidden="true"
        className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-accent text-sm font-medium text-accent-foreground grayscale"
      >
        {(h.name.trim()[0] ?? '?').toUpperCase()}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{h.name}</p>
        <p className="truncate text-xs text-muted-foreground">
          Arkiverad {h.archivedAt ? shortDate(h.archivedAt) : ''}
        </p>
      </div>
      {h.isOwner && (
        <Button
          variant="secondary"
          size="sm"
          className="rounded-full text-primary"
          onClick={onRestore}
          disabled={restore.isPending}
        >
          {restore.isPending && <Loader2Icon className="animate-spin" />}
          Återställ
        </Button>
      )}
    </Card>
  )
}

/** Invite someone to the active household (ADR-0005: any member can invite). */
function InviteSection({
  householdId,
  householdName,
}: {
  householdId: string | undefined
  householdName: string | undefined
}) {
  const [email, setEmail] = useState('')
  const sendInvite = useSendInvite(householdId)
  const invites = useHouseholdInvites(householdId)

  if (!householdId) return null

  async function onInvite() {
    const trimmed = email.trim()
    if (!trimmed) {
      toast('Skriv en e-postadress')
      return
    }
    try {
      const invite = await sendInvite.mutateAsync({ email: trimmed })
      toast(
        invite.emailSent
          ? `Inbjudan skickad till ${trimmed}`
          : `Inbjudan skapad men mejlet kunde inte skickas till ${trimmed} — försök igen senare`,
      )
      setEmail('')
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <div className="flex flex-col gap-2 border-t border-border pt-3">
      <p className="text-xs font-semibold uppercase tracking-[0.09em] text-muted-foreground">
        Bjud in till {householdName ?? 'hushållet'}
      </p>
      <InvitableContacts householdId={householdId} />
      <div className="flex gap-2">
        <Input
          aria-label="E-post"
          placeholder="namn@exempel.se"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="h-9"
        />
        <Button type="button" size="sm" onClick={onInvite} disabled={sendInvite.isPending}>
          {sendInvite.isPending && <Loader2Icon className="animate-spin" />}
          Skicka
        </Button>
      </div>
      {invites.data && invites.data.length > 0 && (
        <ul className="flex flex-col gap-1 text-xs text-muted-foreground">
          {invites.data.map((i) => (
            <li key={i.id}>{i.email} — väntar på svar</li>
          ))}
        </ul>
      )}
    </div>
  )
}

/**
 * Screen 4 (contacts-phone-sms spec): reuse a saved contact instead of re-typing. Lists the acting user's
 * contacts with their status for this household and lets them invite an "invitable" one with a
 * tap. "member"/"pending" rows are shown but not actionable. The wishlist payoff.
 */
function InvitableContacts({ householdId }: { householdId: string }) {
  const contacts = useInvitableContacts(householdId)
  const inviteContact = useInviteContactToHousehold(householdId)

  const list = contacts.data ?? []
  if (list.length === 0) return null

  const invite = async (c: InvitableContactDto) => {
    try {
      await inviteContact.mutateAsync({ contactMemberId: c.memberId })
      toast(`Inbjudan skickad till ${c.name}`)
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <p className="text-[11px] text-muted-foreground">Från dina kontakter</p>
      <div className="flex flex-col gap-1.5">
        {list.map((c) => (
          <div key={c.memberId} className="flex items-center gap-2.5">
            <MemberAvatar name={c.name} avatarColor={c.avatarColor} size="sm" />
            <span className="min-w-0 flex-1 truncate text-sm">{c.name}</span>
            {c.status === 'member' ? (
              <span className="rounded-full bg-muted px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Med
              </span>
            ) : c.status === 'pending' ? (
              <span className="rounded-full bg-muted px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
                Väntar
              </span>
            ) : (
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={inviteContact.isPending}
                onClick={() => invite(c)}
              >
                Bjud in
              </Button>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
