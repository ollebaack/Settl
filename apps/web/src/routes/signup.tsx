/**
 * Skapa konto (/signup) — ADR-0011. Open self-signup; joining a household still
 * requires an invite (sent from household settings after login). Visuals/copy
 * from the "Settl Sign In" design export.
 */
import { useState } from 'react'
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { AuthError, AuthLayout } from '@/components/auth-layout'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useRegister } from '@/lib/queries'

export const Route = createFileRoute('/signup')({
  component: SignupPage,
})

function SignupPage() {
  const navigate = useNavigate()
  const register = useRegister()
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  async function onSubmit() {
    if (!name.trim()) return setError('Ange ditt namn.')
    if (!email.includes('@')) return setError('Ange en giltig e-postadress.')
    if (password.length < 8) return setError('Lösenordet behöver minst 8 tecken.')

    setError('')
    try {
      await register.mutateAsync({ name: name.trim(), email: email.trim(), password })
      navigate({ to: '/verify-email' })
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
      <div>
        <p className="text-lg font-bold tracking-tight">Skapa konto</p>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">
          Ett konto per person — hushåll och grupper delar ni på sen.
        </p>
      </div>

      {error && <AuthError>{error}</AuthError>}

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="name" className="text-[11.5px] font-semibold tracking-wide text-muted-foreground uppercase">
          Namn
        </Label>
        <Input
          id="name"
          autoComplete="name"
          placeholder="Förnamn räcker"
          value={name}
          onChange={(e) => {
            setName(e.target.value)
            setError('')
          }}
        />
      </div>

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
          autoComplete="new-password"
          placeholder="Minst 8 tecken"
          value={password}
          onChange={(e) => {
            setPassword(e.target.value)
            setError('')
          }}
        />
      </div>

      <Button type="button" onClick={onSubmit} disabled={register.isPending} className="mt-1 h-12 w-full">
        {register.isPending && <Loader2Icon className="animate-spin" />}
        {register.isPending ? 'Skapar konto…' : 'Skapa konto'}
      </Button>

      <p className="text-center text-[11.5px] leading-relaxed text-muted-foreground">
        Genom att skapa konto godkänner du villkoren.
      </p>
    </AuthLayout>
  )
}
