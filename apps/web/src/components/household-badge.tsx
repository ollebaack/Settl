/**
 * A household's identity badge — deliberately a ROUNDED SQUARE.
 *
 * Settl's bubble shape-language is "circles are people, rounded squares are
 * things" (docs/design/bubble-hierarchy-addendum.md): a household must never
 * read as a person's `MemberAvatar` (a circle). It always wears the soft-mint
 * surface (`bg-accent` + `text-accent-foreground`) with a hairline — the same
 * brand tint for every household, never a member colour.
 */
import { cn } from '@/lib/utils'

/** ~31% corner radius at every size so it stays clearly square, not a circle. */
const SIZE: Record<'sm' | 'md' | 'lg', string> = {
  sm: 'size-[26px] rounded-[8px] text-xs',
  md: 'size-9 rounded-[11px] text-sm',
  lg: 'size-[46px] rounded-[14px] text-lg',
}

export function HouseholdBadge({
  name,
  size = 'sm',
  className,
}: {
  name: string | undefined
  size?: 'sm' | 'md' | 'lg'
  className?: string
}) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex shrink-0 items-center justify-center border border-accent-foreground/15 bg-accent font-semibold text-accent-foreground',
        SIZE[size],
        className,
      )}
    >
      {(name?.trim()[0] ?? 'S').toUpperCase()}
    </span>
  )
}
