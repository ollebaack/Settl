/**
 * The single responsive app shell (implementation-map §3). Desktop (≥980px) =
 * sticky left sidebar; mobile = sticky top header + bottom tab bar with a
 * centre raised "+" FAB. Wraps the router <Outlet/>. Navigation is a custom
 * component (no stock shadcn nav) with an active-dot indicator.
 */
import type { ReactNode } from 'react'
import { Link } from '@tanstack/react-router'
import {
  BellIcon,
  ChevronDownIcon,
  HomeIcon,
  PlusIcon,
  RepeatIcon,
  ScrollTextIcon,
  type LucideIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { ThemeToggle } from '@/components/theme-toggle'
import { UserSwitcher } from '@/components/user-switcher'
import { MemberAvatarStack } from '@/components/member-avatar'
import { cn } from '@/lib/utils'
import { useSheet } from '@/lib/sheet'
import { useActiveHousehold } from '@/lib/active-household'
import { useMembers } from '@/lib/queries'

interface NavItem {
  to: string
  label: string
  mobileLabel: string
  icon: LucideIcon
}

const NAV: NavItem[] = [
  { to: '/', label: 'Hem', mobileLabel: 'Hem', icon: HomeIcon },
  { to: '/ledger', label: 'Loggbok', mobileLabel: 'Loggbok', icon: ScrollTextIcon },
  { to: '/recurring', label: 'På repeat', mobileLabel: 'Repeat', icon: RepeatIcon },
  { to: '/activity', label: 'Aktivitet', mobileLabel: 'Aktivitet', icon: BellIcon },
]

const HELPER_TEXT =
  'Interaktiv prototyp — lägg till en post, gör upp, pausa en återkommande kostnad eller byt hushåll.'

export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto flex min-h-dvh max-w-[1240px] flex-col min-[980px]:grid min-[980px]:grid-cols-[220px_minmax(0,1fr)]">
        <Sidebar />
        <MobileHeader />
        <main className="flex-1 pb-24 min-[980px]:pb-8">
          <div className="mx-auto w-full max-w-md px-4 py-4 min-[980px]:max-w-[640px] min-[980px]:px-8 min-[980px]:py-8">
            {children}
          </div>
        </main>
      </div>
      <MobileTabBar />
    </div>
  )
}

// --- Desktop sidebar --------------------------------------------------------

function Sidebar() {
  const { openSheet } = useSheet()
  const { household } = useActiveHousehold()

  return (
    <aside className="sticky top-0 hidden h-dvh flex-col gap-4 border-r border-border bg-sidebar px-4 py-6 min-[980px]:flex">
      <div className="px-2">
        <p className="font-heading text-xl font-semibold tracking-tight text-foreground">Settl</p>
        <p className="text-xs text-muted-foreground">Hushållets delade anteckningsbok</p>
      </div>

      <Button
        variant="outline"
        className="justify-between"
        onClick={() => openSheet('households')}
      >
        <span className="truncate">{household?.name ?? 'Välj hushåll'}</span>
        <ChevronDownIcon />
      </Button>

      <nav className="flex flex-col gap-1">
        {NAV.map((item) => (
          <NavLink key={item.to} item={item} />
        ))}
      </nav>

      <Button className="mt-1 justify-start" onClick={() => openSheet('add')}>
        <PlusIcon />
        Ny post
      </Button>

      <div className="mt-auto flex flex-col gap-3">
        <p className="px-2 text-xs text-muted-foreground text-balance">{HELPER_TEXT}</p>
        <Separator />
        <div className="flex items-center justify-between gap-2">
          <UserSwitcher />
          <ThemeToggle />
        </div>
      </div>
    </aside>
  )
}

function NavLink({ item }: { item: NavItem }) {
  const Icon = item.icon
  return (
    <Link
      to={item.to}
      activeOptions={{ exact: item.to === '/' }}
      className="rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {({ isActive }) => (
        <span
          className={cn(
            'flex items-center gap-2.5 rounded-xl px-3 py-2 text-sm transition-colors',
            isActive
              ? 'bg-accent font-medium text-foreground'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground',
          )}
        >
          <Icon className="size-4" />
          <span className="flex-1">{item.label}</span>
          {isActive && <span className="size-1.5 rounded-full bg-primary" />}
        </span>
      )}
    </Link>
  )
}

// --- Mobile top header ------------------------------------------------------

function MobileHeader() {
  const { openSheet } = useSheet()
  const { household, householdId } = useActiveHousehold()
  const { data: members } = useMembers(householdId)

  return (
    <header className="sticky top-0 z-30 flex items-center justify-between gap-2 border-b border-border bg-background/90 px-4 py-3 backdrop-blur min-[980px]:hidden">
      <button
        type="button"
        onClick={() => openSheet('households')}
        className="flex items-center gap-2 rounded-full bg-muted px-3 py-1.5 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span className="flex size-5 items-center justify-center rounded-full bg-primary text-xs font-medium text-primary-foreground">
          {(household?.name?.[0] ?? 'S').toUpperCase()}
        </span>
        <span className="max-w-[160px] truncate font-medium">{household?.name ?? 'Settl'}</span>
        <ChevronDownIcon className="size-4 text-muted-foreground" />
      </button>
      {members && members.length > 0 && (
        <MemberAvatarStack
          members={members.map((m) => ({ name: m.name, avatarColor: m.avatarColor }))}
        />
      )}
    </header>
  )
}

// --- Mobile bottom tab bar --------------------------------------------------

function MobileTabBar() {
  const { openSheet } = useSheet()

  return (
    <nav className="fixed inset-x-0 bottom-0 z-30 grid grid-cols-[1fr_1fr_66px_1fr_1fr] items-center border-t border-border bg-background/95 px-2 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] backdrop-blur min-[980px]:hidden">
      <TabLink item={NAV[0]} />
      <TabLink item={NAV[1]} />
      <div className="flex justify-center">
        <button
          type="button"
          aria-label="Ny post"
          onClick={() => openSheet('add')}
          className="-translate-y-3 flex size-[52px] items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg shadow-primary/30 outline-none transition-transform focus-visible:ring-2 focus-visible:ring-ring active:translate-y-[-10px]"
        >
          <PlusIcon className="size-6" />
        </button>
      </div>
      <TabLink item={NAV[2]} />
      <TabLink item={NAV[3]} />
    </nav>
  )
}

function TabLink({ item }: { item: NavItem }) {
  const Icon = item.icon
  return (
    <Link
      to={item.to}
      activeOptions={{ exact: item.to === '/' }}
      className="outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {({ isActive }) => (
        <span
          className={cn(
            'flex flex-col items-center gap-1 py-1 text-[11px] transition-colors',
            isActive ? 'text-foreground' : 'text-muted-foreground',
          )}
        >
          <Icon className="size-5" />
          <span className="leading-none">{item.mobileLabel}</span>
          <span
            className={cn(
              'size-1 rounded-full',
              isActive ? 'bg-primary' : 'bg-transparent',
            )}
          />
        </span>
      )}
    </Link>
  )
}
