/**
 * Bekräfta e-post callback (/confirm-email?userId=…&token=…) — the link landing page from
 * the verification email (email-verification decision made alongside ADR-0011). No design
 * export covers this screen; styled to match the other auth cards.
 */
import { Link, createFileRoute } from '@tanstack/react-router'
import { CheckCircle2Icon, XCircleIcon } from 'lucide-react'
import { AuthLayout } from '@/components/auth-layout'
import { LoadingState } from '@/components/screen-states'
import { useConfirmEmail } from '@/lib/queries'

export const Route = createFileRoute('/confirm-email')({
  validateSearch: (search: Record<string, unknown>): { userId: string | undefined; token: string | undefined } => ({
    userId: typeof search.userId === 'string' ? search.userId : undefined,
    token: typeof search.token === 'string' ? search.token : undefined,
  }),
  component: ConfirmEmailPage,
})

function ConfirmEmailPage() {
  const { userId, token } = Route.useSearch()
  const confirm = useConfirmEmail(userId, token)

  if (!userId || !token) {
    return (
      <AuthLayout>
        <FailureContent />
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      {confirm.isPending ? (
        <LoadingState hero rows={1} />
      ) : confirm.isError ? (
        <FailureContent />
      ) : (
        <div className="pt-1 text-center">
          <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <CheckCircle2Icon className="size-6" />
          </div>
          <p className="text-lg font-bold tracking-tight">E-post bekräftad</p>
          <p className="mt-1 text-[13px] text-muted-foreground">Du kan nu logga in och komma igång.</p>
          <Link to="/login" className="mt-4 inline-block font-semibold text-primary">
            Till inloggning
          </Link>
        </div>
      )}
    </AuthLayout>
  )
}

function FailureContent() {
  return (
    <div className="pt-1 text-center">
      <div className="mx-auto mb-3 flex size-12 items-center justify-center rounded-2xl bg-destructive/10 text-destructive">
        <XCircleIcon className="size-6" />
      </div>
      <p className="text-lg font-bold tracking-tight">Länken är ogiltig eller har gått ut</p>
      <p className="mt-1 text-[13px] text-muted-foreground">
        Logga in och be om en ny länk från sidan för att bekräfta e-post.
      </p>
      <Link to="/login" className="mt-4 inline-block font-semibold text-primary">
        Till inloggning
      </Link>
    </div>
  )
}
