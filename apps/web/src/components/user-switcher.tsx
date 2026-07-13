/**
 * Dev-only "acting user" switcher (impersonate a seeded member). Gated behind
 * import.meta.env.DEV — never shipped in production. Backs the X-Settl-User
 * header (tech-debt 0003 / api-contract §6). Renders nothing if not in dev.
 */
import { UsersIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { MemberAvatar } from '@/components/member-avatar'
import { useDevUser } from '@/lib/dev-user'
import { cn } from '@/lib/utils'

export function UserSwitcher({ className }: { className?: string }) {
  const { users, memberId, member, setMemberId } = useDevUser()
  if (!import.meta.env.DEV) return null

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="sm" className={cn('gap-2', className)} />
        }
      >
        <UsersIcon />
        <span className="truncate">{member?.name ?? 'Dev-användare'}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-52">
        <DropdownMenuRadioGroup
          value={memberId ?? ''}
          onValueChange={(value) => setMemberId(value)}
        >
          <DropdownMenuLabel>Agera som (dev)</DropdownMenuLabel>
          <DropdownMenuSeparator />
          {users.map((u) => (
            <DropdownMenuRadioItem key={u.id} value={u.id}>
              <MemberAvatar name={u.name} avatarColor={u.avatarColor} size="sm" />
              <span className="truncate">{u.name}</span>
            </DropdownMenuRadioItem>
          ))}
          {users.length === 0 && (
            <DropdownMenuItem disabled>Inga användare</DropdownMenuItem>
          )}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
