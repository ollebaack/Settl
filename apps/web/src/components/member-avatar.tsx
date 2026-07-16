/**
 * A member avatar: shadcn Avatar wrapper showing the member's personalization on
 * their `avatarColor`. The colour is member DATA from the API (not a theme token),
 * so it is applied via inline style. When `avatarEmoji` is set (ADR-0019) it is
 * rendered centered on the colour; otherwise the letter initial is shown, with the
 * readable ink derived from the colour's luminance so it is legible in both themes.
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

/** MemberAvatar sizes. `xl` (78px) is the profile-page person circle (profile-addendum §2.1). */
export type MemberAvatarSize = 'sm' | 'default' | 'lg' | 'xl'

// The underlying ui Avatar only knows sm/default/lg. `xl` maps to `default` (not `lg`) so
// no `data-[size=lg]:size-10` rule is emitted: that attribute-selector would out-specify the
// unconditional `size-[78px]` box override below and shrink the profile avatar back to 40px.
const UI_SIZE: Record<MemberAvatarSize, 'sm' | 'default' | 'lg'> = {
  sm: 'sm',
  default: 'default',
  lg: 'lg',
  xl: 'default',
}
const BOX_CLASS: Partial<Record<MemberAvatarSize, string>> = { xl: 'size-[78px]' }
const INITIAL_CLASS: Partial<Record<MemberAvatarSize, string>> = { xl: 'text-[31px]' }
const EMOJI_CLASS: Record<MemberAvatarSize, string> = {
  sm: 'text-[13px]',
  default: 'text-[17px]',
  lg: 'text-[22px]',
  xl: 'text-[40px]',
}

export function MemberAvatar({
  name,
  avatarColor,
  avatarEmoji,
  size = 'default',
  isYou = false,
  className,
}: {
  name: string
  avatarColor: string
  /** Optional emoji shown in place of the letter initial (ADR-0019). Null/empty → initial. */
  avatarEmoji?: string | null
  size?: MemberAvatarSize
  /** Mark this as the acting user's own bubble: a thin accent ring so you can
   *  spot yourself at a glance (docs/design/bubble-hierarchy-addendum.md). */
  isYou?: boolean
  className?: string
}) {
  const initial = (name.trim()[0] ?? '?').toUpperCase()
  const emoji = avatarEmoji?.trim() || null
  return (
    <Avatar
      size={UI_SIZE[size]}
      className={cn(
        BOX_CLASS[size],
        isYou && 'ring-2 ring-primary ring-offset-2 ring-offset-background',
        className,
      )}
    >
      <AvatarFallback
        className={cn('font-medium', INITIAL_CLASS[size])}
        // Emoji glyphs carry their own colour, so only the initial needs readable ink.
        style={{ backgroundColor: avatarColor, color: emoji ? undefined : readableInk(avatarColor) }}
      >
        {emoji ? (
          <span aria-hidden="true" className={cn('leading-none', EMOJI_CLASS[size])}>
            {emoji}
          </span>
        ) : (
          initial
        )}
      </AvatarFallback>
    </Avatar>
  )
}

/** Overlapping stack of member avatars (decorative header cluster). The acting
 *  user (`isYou`) wears the accent ring and sits on top so its ring stays whole. */
export function MemberAvatarStack({
  members,
  max = 4,
  size = 'sm',
  className,
}: {
  members: { name: string; avatarColor: string; avatarEmoji?: string | null; isYou?: boolean }[]
  max?: number
  size?: MemberAvatarSize
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
          avatarEmoji={m.avatarEmoji}
          size={size}
          isYou={m.isYou}
          className={m.isYou ? 'z-10' : 'ring-2 ring-background'}
        />
      ))}
    </div>
  )
}
