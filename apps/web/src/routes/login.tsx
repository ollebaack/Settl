/**
 * Logga in (/login) — ADR-0011. Visuals/copy from the "Settl Sign In" design
 * export; contract (query-token invites, email+password only) is ADR-0011's,
 * not the export's proposed magic-link/share-link model (deferred).
 */
import { useState } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { AuthError, AuthLayout } from '@/components/auth-layout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useLogin } from '@/lib/queries'

export const Route = createFileRoute('/login')({
  component: LoginPage,
})

function LoginPage() {
  const navigate = useNavigate()
  const login = useLogin()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

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

      <Button type="button" onClick={onSubmit} disabled={login.isPending} className="mt-1 h-12 w-full">
        {login.isPending && <Loader2Icon className="animate-spin" />}
        {login.isPending ? 'Loggar in…' : 'Logga in'}
      </Button>
    </AuthLayout>
  )
}
