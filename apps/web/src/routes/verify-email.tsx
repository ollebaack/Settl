/**
 * Bekräfta e-post (/verify-email) — email-verification decision made alongside ADR-0005.
 * Shown to a signed-in-but-unconfirmed member (RequireAuth routes them here instead of
 * into the app) so they can resend the confirmation link or log out to try another account.
 */
import { useState } from 'react'
import { Navigate, createFileRoute, useNavigate } from '@tanstack/react-router'
import { Loader2Icon, MailCheckIcon } from 'lucide-react'
import { AuthError, AuthLayout } from '@/components/auth-layout'
import { Button } from '@/components/ui/button'
import { LoadingState } from '@/components/screen-states'
import { useLogout, useMe, useResendVerification } from '@/lib/queries'

export const Route = createFileRoute('/verify-email')({
  component: VerifyEmailPage,
})

function VerifyEmailPage() {
  const { data: me, isPending } = useMe()
  const resend = useResendVerification()
  const logout = useLogout()
  const navigate = useNavigate()
  const [sent, setSent] = useState(false)
  const [error, setError] = useState('')

  if (isPending) return <LoadingState hero rows={2} />
  if (!me) return <Navigate to="/login" />
  if (me.emailConfirmed) return <Navigate to="/" />

  async function onResend() {
    setError('')
    try {
      await resend.mutateAsync()
      setSent(true)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  async function onLogout() {
    await logout.mutateAsync()
    navigate({ to: '/login' })
  }

  return (
    <AuthLayout>
      <div className="pt-1 text-center">
        <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
          <MailCheckIcon className="size-6" />
        </div>
        <p className="text-lg font-bold tracking-tight">Bekräfta din e-postadress</p>
        <p className="mt-1 text-[13px] text-muted-foreground">
          Vi har skickat en länk till <span className="font-semibold text-foreground">{me.name}</span>s
          e-post. Klicka på den för att komma igång.
        </p>
      </div>

      {error && <AuthError>{error}</AuthError>}
      {sent && !error && (
        <p className="rounded-xl bg-accent px-3 py-2 text-center text-[12.5px] font-semibold text-accent-foreground">
          Ny länk skickad.
        </p>
      )}

      <Button type="button" onClick={onResend} disabled={resend.isPending} className="mt-1 h-12 w-full">
        {resend.isPending && <Loader2Icon className="animate-spin" />}
        Skicka länken igen
      </Button>

      <Button type="button" variant="ghost" onClick={onLogout} className="h-10 w-full text-muted-foreground">
        Logga ut
      </Button>
    </AuthLayout>
  )
}
