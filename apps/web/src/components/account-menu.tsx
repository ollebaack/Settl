/**
 * Shows who's logged in and lets them log out (ADR-0011 — replaces the dev-only
 * user switcher this app used before real auth).
 */
import { useNavigate } from '@tanstack/react-router'
import { LogOutIcon, Settings2Icon, UsersIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { MemberAvatar } from '@/components/member-avatar'
import { useMe, useLogout } from '@/lib/queries'
import { cn } from '@/lib/utils'

export function AccountMenu({ className }: { className?: string }) {
  const { data: me } = useMe()
  const logout = useLogout()
  const navigate = useNavigate()

  if (!me) return null

  async function onLogout() {
    try {
      await logout.mutateAsync()
      navigate({ to: '/login' })
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="sm"
            aria-label={`Konto: ${me.name}`}
            className={cn('gap-2', className)}
          />
        }
      >
        <MemberAvatar name={me.name} avatarColor={me.avatarColor} size="sm" isYou />
        {/* Name hidden below the desktop breakpoint — mobile header has no room,
            but logout must still be reachable there (mobile-first); the aria-label
            above keeps the accessible name stable regardless. */}
        <span className="hidden truncate min-[980px]:inline" aria-hidden="true">
          {me.name}
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-52">
        <DropdownMenuGroup>
          <DropdownMenuLabel>{me.name}</DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem onClick={() => navigate({ to: '/kontakter' })}>
            <UsersIcon />
            Kontakter
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => navigate({ to: '/household', search: {} })}>
            <Settings2Icon />
            Hantera hushåll
          </DropdownMenuItem>
          <DropdownMenuItem onClick={onLogout}>
            <LogOutIcon />
            Logga ut
          </DropdownMenuItem>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
