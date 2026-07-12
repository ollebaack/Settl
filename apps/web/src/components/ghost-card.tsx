/**
 * Dashed "ghost" card for upcoming auto-posts (På gång). 1.5px dashed border,
 * soft look. On Home mobile these are clickable → Recurring detail (prototype
 * behaviour, implementation-map ambiguity #13).
 */
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export function GhostCard({
  children,
  onClick,
  className,
}: {
  children: ReactNode
  onClick?: () => void
  className?: string
}) {
  const classes = cn(
    'rounded-2xl border-[1.5px] border-dashed border-border bg-transparent p-3 text-left',
    onClick && 'cursor-pointer transition-colors hover:bg-muted/40',
    className,
  )
  if (onClick) {
    return (
      <button type="button" onClick={onClick} className={classes}>
        {children}
      </button>
    )
  }
  return <div className={classes}>{children}</div>
}
