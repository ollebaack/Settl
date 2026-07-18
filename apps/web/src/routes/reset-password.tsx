/**
 * Återställ lösenord (/reset-password?userId=…&token=…) — the link landing page from the
 * "Glömt?" flow (docs/design/auth-onboarding-addendum.md §2.1). Needs its own route/form
 * (not covered by the addendum) because the addendum's sent-state assumed a passwordless
 * magic-link sign-in, which ADR-0005 deferred — resetting still means choosing a new
 * password, so this borrows the invite-accept screen's password-field pattern instead.
 */
import { useState } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { AuthError, AuthLayout } from '@/components/auth-layout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useResetPassword } from '@/lib/queries'

export const Route = createFileRoute('/reset-password')({
  validateSearch: (search: Record<string, unknown>): { userId: string | undefined; token: string | undefined } => ({
    userId: typeof search.userId === 'string' ? search.userId : undefined,
    token: typeof search.token === 'string' ? search.token : undefined,
  }),
  component: ResetPasswordPage,
})

function ResetPasswordPage() {
  const { userId, token } = Route.useSearch()
  const navigate = useNavigate()
  const reset = useResetPassword()
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  if (!userId || !token) {
    return (
      <AuthLayout>
        <div className="pt-1 text-center text-sm text-muted-foreground">
          Länken är ogiltig eller har gått ut.{' '}
          <Link to="/login" className="font-semibold text-primary">
            Till inloggning
          </Link>
        </div>
      </AuthLayout>
    )
  }

  async function onSubmit() {
    if (password.length < 8) return setError('Lösenordet behöver minst 8 tecken.')

    setError('')
    try {
      await reset.mutateAsync({ userId: userId!, token: token!, newPassword: password })
      navigate({ to: '/' })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Länken är ogiltig eller har gått ut.')
    }
  }

  return (
    <AuthLayout>
      <div>
        <p className="text-lg font-bold tracking-tight">Välj ett nytt lösenord</p>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">Du loggas in direkt efteråt.</p>
      </div>

      {error && <AuthError>{error}</AuthError>}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="password" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
          Nytt lösenord
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

      <Button type="button" onClick={onSubmit} disabled={reset.isPending} className="mt-1 h-12 w-full">
        {reset.isPending && <Loader2Icon className="animate-spin" />}
        {reset.isPending ? 'Sparar…' : 'Spara lösenord'}
      </Button>
    </AuthLayout>
  )
}
