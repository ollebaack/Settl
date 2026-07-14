/**
 * Reusable screen states: loading skeletons, empty state, and an inline error
 * card with retry. Screens must surface errors (apps/web/CLAUDE.md) — no silent
 * catches, no infinite spinners.
 */
import type { ReactNode } from 'react'
import { RefreshCwIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

/** Stacked row skeletons for a loading list; `hero` adds a tall block on top. */
export function LoadingState({
  rows = 3,
  hero = false,
  className,
}: {
  rows?: number
  hero?: boolean
  className?: string
}) {
  return (
    <div className={cn('flex flex-col gap-3', className)} aria-busy="true">
      {hero && <Skeleton className="h-28 w-full rounded-2xl" />}
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-16 w-full rounded-2xl" />
      ))}
    </div>
  )
}

/** Centered muted message for empty collections. */
export function EmptyState({
  children,
  icon,
  className,
}: {
  children: ReactNode
  icon?: ReactNode
  className?: string
}) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2 px-6 py-10 text-center text-sm text-muted-foreground',
        className,
      )}
    >
      {icon}
      <p className="text-balance">{children}</p>
    </div>
  )
}

/** Shown instead of a screen's content when the user belongs to no household yet. */
export function NoHouseholdState({
  onCreate,
  className,
}: {
  onCreate: () => void
  className?: string
}) {
  return (
    <Card className={cn('flex flex-col items-center gap-3 p-8 text-center', className)}>
      <p className="text-sm font-semibold">Inget hushåll än</p>
      <p className="text-sm text-muted-foreground text-balance">
        Skapa ett hushåll för att börja dela utgifter, lån och återkommande kostnader.
      </p>
      <Button onClick={onCreate}>Skapa hushåll</Button>
    </Card>
  )
}

/** Inline error card with a retry action. */
export function ErrorState({
  error,
  onRetry,
  className,
}: {
  error?: unknown
  onRetry?: () => void
  className?: string
}) {
  const message =
    error instanceof Error ? error.message : 'Något gick fel. Försök igen.'
  return (
    <Card className={cn('flex flex-col items-center gap-3 p-6 text-center', className)}>
      <p className="text-sm text-muted-foreground text-balance">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RefreshCwIcon />
          Försök igen
        </Button>
      )}
    </Card>
  )
}
