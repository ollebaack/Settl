/**
 * A member avatar: shadcn Avatar wrapper showing the member's initial on their
 * `avatarColor`. The colour is member DATA from the API (not a theme token), so
 * it is applied via inline style; the readable ink is derived from its
 * luminance so the initial is legible in both light and dark themes.
 */
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'

/** Pick a dark or light ink for text drawn on `hex` (derived from member data). */
function readableInk(hex: string): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return '#1c2620'
  const n = parseInt(m[1], 16)
  const r = (n >> 16) & 255
  const g = (n >> 8) & 255
  const b = n & 255
  // Perceived luminance (sRGB approximation).
  const lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255
  return lum > 0.6 ? '#1c2620' : '#f4f6f2'
}

export function MemberAvatar({
  name,
  avatarColor,
  size = 'default',
  className,
}: {
  name: string
  avatarColor: string
  size?: 'sm' | 'default' | 'lg'
  className?: string
}) {
  const initial = (name.trim()[0] ?? '?').toUpperCase()
  return (
    <Avatar size={size} className={className}>
      <AvatarFallback
        className="font-medium"
        style={{ backgroundColor: avatarColor, color: readableInk(avatarColor) }}
      >
        {initial}
      </AvatarFallback>
    </Avatar>
  )
}

/** Overlapping stack of member avatars (decorative header cluster). */
export function MemberAvatarStack({
  members,
  max = 4,
  size = 'sm',
  className,
}: {
  members: { name: string; avatarColor: string }[]
  max?: number
  size?: 'sm' | 'default' | 'lg'
  className?: string
}) {
  const shown = members.slice(0, max)
  return (
    <div className={cn('flex -space-x-2', className)}>
      {shown.map((m, i) => (
        <MemberAvatar
          key={`${m.name}-${i}`}
          name={m.name}
          avatarColor={m.avatarColor}
          size={size}
          className="ring-2 ring-background"
        />
      ))}
    </div>
  )
}
