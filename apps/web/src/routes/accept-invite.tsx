/**
 * Acceptera inbjudan (/accept-invite?token=…) — ADR-0011 (email) + ADR-0019 (SMS/contact).
 *
 * The invite may be household-scoped or contact-only (no household to join, just a contact
 * edge), and delivered by email or SMS. Email invites bind to a known address; SMS invites
 * carry no identity, so the invitee supplies their own email (email stays the sole login).
 * We never reveal whether a number/email is already on Settl.
 */
import { cloneElement, useState } from 'react'
import type { ReactElement, ReactNode } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { AuthError, AuthLayout } from '@/components/auth-layout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { LoadingState } from '@/components/screen-states'
import { useAcceptInvite, useInvitePreview, useLogin, useMe } from '@/lib/queries'

export const Route = createFileRoute('/accept-invite')({
  validateSearch: (search: Record<string, unknown>): { token: string | undefined } => ({
    token: typeof search.token === 'string' ? search.token : undefined,
  }),
  component: AcceptInvitePage,
})

const FIELD_LABEL =
  'text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase'

function AcceptInvitePage() {
  const { token } = Route.useSearch()
  const navigate = useNavigate()
  const { data: me } = useMe()
  const preview = useInvitePreview(token)
  const login = useLogin()
  const accept = useAcceptInvite(token)

  const [name, setName] = useState('')
  const [smsEmail, setSmsEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  if (!token) {
    return <CenteredMessage>Ogiltig inbjudningslänk.</CenteredMessage>
  }
  if (preview.isPending) {
    return <LoadingState hero rows={2} />
  }
  if (preview.isError || !preview.data) {
    return <CenteredMessage>Inbjudan hittades inte eller har gått ut.</CenteredMessage>
  }

  const { householdName, inviterName, email, hasAccount, channel } = preview.data
  const isSms = channel === 'sms'
  const busy = login.isPending || accept.isPending

  async function goHome() {
    navigate({ to: '/' })
  }

  // Already signed in → one tap accepts as the current account (both channels).
  async function onAcceptLoggedIn() {
    setError('')
    try {
      await accept.mutateAsync({ name: null, email: null, password: null })
      await goHome()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  // Email invite, existing account, not signed in → log in then accept.
  async function onAcceptExisting() {
    setError('')
    try {
      if (email) await login.mutateAsync({ email, password })
      await accept.mutateAsync({ name: null, email: null, password: null })
      await goHome()
    } catch {
      setError('Fel lösenord, eller du är inloggad som fel konto.')
    }
  }

  // Email invite, no account yet → create it (email is bound server-side).
  async function onAcceptNewEmail() {
    if (!name.trim()) return setError('Ange ditt namn först.')
    if (password.length < 8) return setError('Lösenordet behöver minst 8 tecken.')
    setError('')
    try {
      await accept.mutateAsync({ name: name.trim(), email: null, password })
      await goHome()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  // SMS invite, not signed in → the invitee brings their own email as the new account's identity.
  async function onAcceptNewSms() {
    if (!name.trim()) return setError('Ange ditt namn först.')
    if (!/^\S+@\S+\.\S+$/.test(smsEmail.trim())) return setError('Ange en giltig e-postadress.')
    if (password.length < 8) return setError('Lösenordet behöver minst 8 tecken.')
    setError('')
    try {
      await accept.mutateAsync({ name: name.trim(), email: smsEmail.trim(), password })
      await goHome()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <AuthLayout
      footer={
        <>
          Har du redan ett konto?{' '}
          <Link to="/login" className="font-semibold text-primary">
            Logga in
          </Link>
        </>
      }
    >
      <div className="pt-1 text-center">
        <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-xl font-bold text-primary">
          {inviterName.trim()[0]?.toUpperCase()}
        </div>
        <p className="text-lg font-bold tracking-tight">
          {householdName ? `${inviterName} har bjudit in dig` : `${inviterName} vill lägga till dig`}
        </p>
        <p className="mt-1 text-[13px] text-muted-foreground">
          {householdName ? (
            <>
              till hushållet <span className="font-semibold text-foreground">{householdName}</span>
            </>
          ) : (
            'som kontakt på Settl'
          )}
        </p>
      </div>

      {error && <AuthError>{error}</AuthError>}

      {me && (isSms || hasAccount) ? (
        // Signed in AND acceptable as this account (SMS has no bound identity; an email invite
        // whose account already exists is presumably this one) → accept directly. A new-email
        // invite must still create ITS account below, even if someone else is logged in.
        <Button type="button" onClick={onAcceptLoggedIn} disabled={busy} className="mt-1 h-12 w-full">
          {busy && <Loader2Icon className="animate-spin" />}
          {householdName ? 'Acceptera inbjudan' : 'Lägg till som kontakt'}
        </Button>
      ) : isSms ? (
        // SMS invite, not signed in → create an account with the invitee's own email.
        <>
          <p className="border-t border-border pt-3.5 text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
            Skapa ditt konto för att fortsätta
          </p>
          <Field id="name" label="Ditt namn">
            <Input
              autoComplete="name"
              placeholder="Så här syns du i loggboken"
              value={name}
              onChange={(e) => {
                setName(e.target.value)
                setError('')
              }}
            />
          </Field>
          <Field id="email" label="E-post">
            <Input
              type="email"
              autoComplete="email"
              placeholder="namn@exempel.se"
              value={smsEmail}
              onChange={(e) => {
                setSmsEmail(e.target.value)
                setError('')
              }}
            />
          </Field>
          <Field id="password" label="Lösenord">
            <Input
              type="password"
              autoComplete="new-password"
              placeholder="Minst 8 tecken"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value)
                setError('')
              }}
            />
          </Field>
          <Button type="button" onClick={onAcceptNewSms} disabled={busy} className="mt-1 h-12 w-full">
            {busy && <Loader2Icon className="animate-spin" />}
            {busy ? 'Skapar konto…' : 'Skapa konto & fortsätt'}
          </Button>
        </>
      ) : hasAccount ? (
        // Email invite, existing account → log in and accept.
        <>
          <Field id="email" label="E-post">
            <Input value={email ?? ''} disabled />
          </Field>
          <Field id="password" label="Lösenord">
            <Input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value)
                setError('')
              }}
            />
          </Field>
          <Button type="button" onClick={onAcceptExisting} disabled={busy} className="mt-1 h-12 w-full">
            {busy && <Loader2Icon className="animate-spin" />}
            Logga in och acceptera
          </Button>
        </>
      ) : (
        // Email invite, no account yet → create it (email bound to the invite).
        <>
          <p className="border-t border-border pt-3.5 text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
            Skapa ditt konto för att {householdName ? 'gå med' : 'fortsätta'}
          </p>
          <Field id="email" label="E-post">
            <Input value={email ?? ''} disabled />
          </Field>
          <Field id="name" label="Ditt namn">
            <Input
              autoComplete="name"
              placeholder="Så här syns du i loggboken"
              value={name}
              onChange={(e) => {
                setName(e.target.value)
                setError('')
              }}
            />
          </Field>
          <Field id="password" label="Lösenord">
            <Input
              type="password"
              autoComplete="new-password"
              placeholder="Minst 8 tecken"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value)
                setError('')
              }}
            />
          </Field>
          <Button type="button" onClick={onAcceptNewEmail} disabled={busy} className="mt-1 h-12 w-full">
            {busy && <Loader2Icon className="animate-spin" />}
            {busy ? 'Skapar konto…' : householdName ? 'Skapa konto & gå med' : 'Skapa konto & fortsätt'}
          </Button>
        </>
      )}
    </AuthLayout>
  )
}

/** Field wrapper that associates the label with its control (so getByLabel / screen readers
 * resolve the accessible name), by injecting a shared id onto the single child input. */
function Field({
  id,
  label,
  children,
}: {
  id: string
  label: string
  children: ReactElement<{ id?: string }>
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id} className={FIELD_LABEL}>
        {label}
      </Label>
      {cloneElement(children, { id })}
    </div>
  )
}

function CenteredMessage({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh items-center justify-center px-4 text-center text-sm text-muted-foreground">
      {children}
    </div>
  )
}
