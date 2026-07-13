/**
 * Shared centered layout for auth pages (ADR-0011; visuals from the
 * "Settl Sign In" design export — logo block + tagline, one card, footer line).
 */
import type { ReactNode } from 'react'
import { Card } from '@/components/ui/card'

export function AuthLayout({ children, footer }: { children: ReactNode; footer?: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center gap-7 px-5 py-8">
      <div className="text-center">
        <div className="mx-auto mb-3.5 flex size-13 items-center justify-center rounded-2xl bg-primary text-2xl font-bold tracking-tight text-primary-foreground">
          S
        </div>
        <p className="font-heading text-[26px] font-bold tracking-tight">Settl</p>
        <p className="mt-1 text-[13px] text-muted-foreground">
          Den delade anteckningsboken — för hushållet, resan eller gänget
        </p>
      </div>

      <Card className="w-full max-w-[380px] gap-4 p-6">{children}</Card>

      {footer && <div className="min-h-5 text-[13px] text-muted-foreground">{footer}</div>}
    </div>
  )
}

/** Inline error banner — auth forms surface errors this way, not toasts
 * (deliberate: they must survive until the user corrects them). */
export function AuthError({ children }: { children: ReactNode }) {
  return (
    <p className="rounded-xl bg-destructive/10 px-3 py-2 text-[12.5px] font-semibold text-destructive">
      {children}
    </p>
  )
}
