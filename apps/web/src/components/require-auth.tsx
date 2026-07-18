/** Wraps a route's page component; redirects to /login when unauthenticated, or to
 * /verify-email when signed in but not yet email-confirmed (ADR-0005). */
import { Navigate } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { LoadingState } from '@/components/screen-states'
import { useMe } from '@/lib/queries'

export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: me, isPending, isError } = useMe()

  if (isPending) return <LoadingState hero rows={3} />
  if (isError || !me) return <Navigate to="/login" />
  if (!me.emailConfirmed) return <Navigate to="/verify-email" />

  return <>{children}</>
}
