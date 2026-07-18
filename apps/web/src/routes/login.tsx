/**
 * Logga in (/login) — ADR-0005. Visuals/copy from the "Settl Sign In" design
 * export; contract (query-token invites, email+password only) is ADR-0005's,
 * not the export's proposed magic-link/share-link model (deferred). The
 * "Glömt?" forgot-password flow (docs/design/auth-onboarding-addendum.md
 * §2.1) IS built as spec'd: an inline card-content swap to a "sent" state,
 * not a separate route — reset-password itself needs its own route only
 * because the deferred magic-link model would otherwise have signed the
 * user straight in from the emailed link.
 */
import { useState } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2Icon, MailCheckIcon } from 'lucide-react'
import { AuthError, AuthLayout } from '@/components/auth-layout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useForgotPassword, useLogin } from '@/lib/queries'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()
  const forgotPassword = useForgotPassword()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [sentTo, setSentTo] = useState<string | null>(null)

  async function onSubmit() {
    if (!email.includes('@')) return setError('Ange en giltig e-postadress.')
    if (!password) return setError('Ange ditt lösenord.')

    setError('')
    try {
      await login.mutateAsync({ email: email.trim(), password })
      navigate({ to: '/' })
    } catch {
      setError('Fel e-post eller lösenord.')
    }
  }

  async function onForgotPassword() {
    if (!email.includes('@')) return setError('Fyll i din e-post så skickar vi en återställningslänk.')

    setError('')
    try {
      await forgotPassword.mutateAsync({ email: email.trim() })
      setSentTo(email.trim())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  if (sentTo) {
    return (
      <AuthLayout>
        <div className="pt-1 text-center">
          <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <MailCheckIcon className="size-6" />
          </div>
          <p className="text-lg font-bold tracking-tight">Kolla din inkorg</p>
          <p className="mt-1 text-[13px] text-muted-foreground">
            Vi har skickat en återställningslänk till <span className="font-mono">{sentTo}</span>
          </p>
        </div>
        <Button type="button" variant="ghost" onClick={() => setSentTo(null)} className="h-10 w-full">
          ← Tillbaka till inloggning
        </Button>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout
      footer={
        <>
          Ny på Settl?{' '}
          <Link to="/signup" className="font-semibold text-primary">
            Skapa konto
          </Link>
        </>
      }
    >
      <div>
        <p className="text-lg font-bold tracking-tight">Välkommen tillbaka</p>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">
          Logga in för att se hur ni ligger till.
        </p>
      </div>

      {error && <AuthError>{error}</AuthError>}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="email" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
          E-post
        </Label>
        <Input
          id="email"
          type="email"
          autoComplete="email"
          placeholder="namn@exempel.se"
          value={email}
          onChange={(e) => {
            setEmail(e.target.value)
            setError('')
          }}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <div className="flex items-baseline justify-between">
          <Label htmlFor="password" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
            Lösenord
          </Label>
          <button
            type="button"
            onClick={onForgotPassword}
            disabled={forgotPassword.isPending}
            className="rounded text-[11.5px] font-semibold text-primary outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            Glömt?
          </button>
        </div>
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

      <Button type="button" onClick={onSubmit} disabled={login.isPending} className="mt-1 h-12 w-full">
        {login.isPending && <Loader2Icon className="animate-spin" />}
        {login.isPending ? 'Loggar in…' : 'Logga in'}
      </Button>
    </AuthLayout>
  )
}
