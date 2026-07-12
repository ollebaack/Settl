/**
 * Renders an integer-minor-unit amount via formatKr in font-mono. Sign is
 * conveyed by colour + label elsewhere; use `intent` to colour the amount.
 * See implementation-map §7 — never inline money formatting.
 */
import { cn } from '@/lib/utils'
import { formatKr } from '@/lib/format'

export type MoneyIntent = 'default' | 'success' | 'destructive' | 'muted'

const intentClass: Record<MoneyIntent, string> = {
  default: 'text-foreground',
  success: 'text-success',
  destructive: 'text-destructive',
  muted: 'text-muted-foreground',
}

export function Money({
  minor,
  intent = 'default',
  className,
}: {
  minor: number | string
  intent?: MoneyIntent
  className?: string
}) {
  return (
    <span className={cn('font-mono tabular-nums', intentClass[intent], className)}>
      {formatKr(minor)}
    </span>
  )
}
