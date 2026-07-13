/**
 * Acceptera inbjudan (/accept-invite?token=…) — ADR-0011. No account yet → sets a
 * password and joins in one step. Existing account → logs in (if needed) then
 * joins. Visuals/copy from the "Settl Sign In" design export, adapted to the
 * token-in-query contract (the export's path-code + member-preview list are a
 * separate, not-yet-grilled invite model).
 */
import { useState } from 'react'
import type { ReactNode } from 'react'
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

function AcceptInvitePage() {
  const { token } = Route.useSearch()
  const navigate = useNavigate()
  const { data: me } = useMe()
  const preview = useInvitePreview(token)
  const login = useLogin()
  const accept = useAcceptInvite(token)

  const [name, setName] = useState('')
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

  const { householdName, inviterName, email, hasAccount } = preview.data
  const busy = login.isPending || accept.isPending

  async function onAcceptExisting() {
    setError('')
    try {
      if (!me) await login.mutateAsync({ email, password })
      await accept.mutateAsync({ name: null, password: null })
      navigate({ to: '/' })
    } catch {
      setError('Fel lösenord, eller du är inloggad som fel konto.')
    }
  }

  async function onAcceptNew() {
    if (!name.trim()) return setError('Ange ditt namn först.')
    if (password.length < 8) return setError('Lösenordet behöver minst 8 tecken.')

    setError('')
    try {
      await accept.mutateAsync({ name: name.trim(), password })
      navigate({ to: '/' })
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
        <p className="text-lg font-bold tracking-tight">{inviterName} har bjudit in dig</p>
        <p className="mt-1 text-[13px] text-muted-foreground">
          till hushållet <span className="font-semibold text-foreground">{householdName}</span>
        </p>
      </div>

      {error && <AuthError>{error}</AuthError>}

      {hasAccount ? (
        <>
          {!me && (
            <>
              <div className="flex flex-col gap-1.5">
                <Label className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
                  E-post
                </Label>
                <Input value={email} disabled />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="password" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
                  Lösenord
                </Label>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value)
                    setError('')
                  }}
                />
              </div>
            </>
          )}
          <Button type="button" onClick={onAcceptExisting} disabled={busy} className="mt-1 h-12 w-full">
            {busy && <Loader2Icon className="animate-spin" />}
            {me ? 'Acceptera inbjudan' : 'Logga in och acceptera'}
          </Button>
        </>
      ) : (
        <>
          <p className="border-t border-border pt-3.5 text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
            Skapa ditt konto för att gå med
          </p>
          <div className="flex flex-col gap-1.5">
            <Label className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
              E-post
            </Label>
            <Input value={email} disabled />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="name" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
              Ditt namn
            </Label>
            <Input
              id="name"
              autoComplete="name"
              placeholder="Så här syns du i loggboken"
              value={name}
              onChange={(e) => {
                setName(e.target.value)
                setError('')
              }}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="password" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
              Lösenord
            </Label>
            <Input
              id="password"
              type="password"
              autoComplete="new-password"
              placeholder="Minst 8 tecken"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value)
                setError('')
              }}
            />
          </div>
          <Button type="button" onClick={onAcceptNew} disabled={busy} className="mt-1 h-12 w-full">
            {busy && <Loader2Icon className="animate-spin" />}
            {busy ? 'Skapar konto…' : 'Skapa konto & gå med'}
          </Button>
        </>
      )}
    </AuthLayout>
  )
}

function CenteredMessage({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh items-center justify-center px-4 text-center text-sm text-muted-foreground">
      {children}
    </div>
  )
}
