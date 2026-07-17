/**
 * The single responsive app shell (implementation-map §3). Desktop (≥980px) =
 * sticky left sidebar; mobile = sticky top header + bottom tab bar with a
 * centre raised "+" FAB. Wraps the router <Outlet/>. Navigation is a custom
 * component (no stock shadcn nav) with an active-dot indicator.
 */
import type { ReactNode } from 'react'
import { Link, useNavigate } from '@tanstack/react-router'
import {
  BarChart3Icon,
  BellIcon,
  BookOpenIcon,
  ChevronDownIcon,
  HomeIcon,
  PlusIcon,
  RepeatIcon,
  type LucideIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { ThemeToggle } from '@/components/theme-toggle'
import { AccountMenu } from '@/components/account-menu'
import { DevBar } from '@/components/dev-bar'
import { HouseholdBadge } from '@/components/household-badge'
import { GhostCard } from '@/components/ghost-card'
import { Money } from '@/components/money'
import { cn } from '@/lib/utils'
import { inDays, shortDate } from '@/lib/format'
import { useSheet } from '@/lib/sheet'
import { useActiveHousehold } from '@/lib/active-household'
import { useNudges, useNudgeTone, useSummary } from '@/lib/queries'
import type { NudgeActionDto, NudgeDto } from '@/lib/api'

/** Uppercase tracked eyebrow used by the desktop right-rail section headers. */
const RAIL_LABEL =
  'text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground'

interface NavItem {
  to: string
  label: string
  mobileLabel: string
  icon: LucideIcon
}

const NAV: NavItem[] = [
  { to: '/', label: 'Hem', mobileLabel: 'Hem', icon: HomeIcon },
  { to: '/hushallet', label: 'Hushållet', mobileLabel: 'Hushållet', icon: BookOpenIcon },
  { to: '/recurring', label: 'På repeat', mobileLabel: 'Repeat', icon: RepeatIcon },
  { to: '/statistik', label: 'Statistik', mobileLabel: 'Statistik', icon: BarChart3Icon },
]

const HELPER_TEXT =
  'Interaktiv prototyp — lägg till en post, gör upp, pausa en återkommande kostnad eller byt hushåll.'

export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-dvh bg-background">
      <div className="mx-auto flex min-h-dvh max-w-[1240px] flex-col min-[980px]:grid min-[980px]:grid-cols-[220px_minmax(0,1fr)_300px]">
        <Sidebar />
        <MobileHeader />
        <main className="flex-1 pb-32 min-[980px]:pb-16">
          <div className="mx-auto w-full max-w-md px-4 py-4 min-[980px]:max-w-[640px] min-[980px]:px-8 min-[980px]:py-8">
            {children}
          </div>
        </main>
        <RightRail />
      </div>
      <div className="fixed inset-x-0 bottom-0 z-40 flex flex-col-reverse">
        <MobileTabBar />
        <DevBar />
      </div>
    </div>
  )
}

// --- Desktop sidebar --------------------------------------------------------

function Sidebar() {
  const { openSheet } = useSheet()
  const { household } = useActiveHousehold()

  return (
    <aside className="sticky top-0 hidden h-dvh flex-col gap-4 border-r border-border bg-sidebar px-4 pt-6 pb-16 min-[980px]:flex">
      <div className="px-2">
        <p className="font-heading text-xl font-semibold tracking-tight text-foreground">Settl</p>
        <p className="text-xs text-muted-foreground">Hushållets delade anteckningsbok</p>
      </div>

      <Button
        variant="outline"
        className="justify-start gap-2.5"
        onClick={() => openSheet('households')}
      >
        <HouseholdBadge name={household?.name} />
        <span className="flex-1 truncate text-left">{household?.name ?? 'Välj hushåll'}</span>
        <ChevronDownIcon className="text-muted-foreground" />
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
          <AccountMenu />
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
          <span
            aria-hidden="true"
            className={cn('size-1.5 rounded-full', isActive ? 'bg-primary' : 'bg-transparent')}
          />
          <Icon className="size-4" />
          <span className="flex-1">{item.label}</span>
        </span>
      )}
    </Link>
  )
}

// --- Mobile top header ------------------------------------------------------

/**
 * Mobile top header (title-integrated). The active household's name IS the
 * screen title on the left (tap → household switcher sheet); the right side is
 * deliberately slim — the bell and the acting user's account avatar only. The
 * member roster and the balance are NOT repeated here: members live on the
 * household cards / Hushållet screen, the balance in the page's net hero. This
 * removes the old header's duplication (the acting user appeared both in a
 * decorative member stack and as the account button) and its fake tap-target.
 */
function MobileHeader() {
  const { openSheet } = useSheet()
  const { household, householdId } = useActiveHousehold()
  const { data: nudges } = useNudges(householdId)
  const hasNudges = (nudges?.length ?? 0) > 0

  return (
    <header className="sticky top-0 z-30 flex items-center justify-between gap-2 border-b border-border bg-background/90 px-4 py-3 backdrop-blur min-[980px]:hidden">
      <button
        type="button"
        onClick={() => openSheet('households')}
        className="-ml-1.5 flex items-center gap-2.5 rounded-xl px-1.5 py-1.5 text-left outline-none transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
      >
        <HouseholdBadge name={household?.name} size="md" />
        <span className="max-w-[190px] truncate text-lg font-semibold tracking-tight">
          {household?.name ?? 'Settl'}
        </span>
        <ChevronDownIcon className="size-4 shrink-0 text-muted-foreground" />
      </button>
      <div className="flex items-center gap-0.5">
        <Link
          to="/activity"
          aria-label="Notiser"
          className="relative grid size-10 place-items-center rounded-full text-muted-foreground outline-none transition-colors hover:bg-muted hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
        >
          <BellIcon className="size-5" />
          {hasNudges && (
            <span
              aria-hidden="true"
              className="absolute right-2 top-2 size-2 rounded-full bg-primary ring-2 ring-background"
            />
          )}
        </Link>
        <AccountMenu />
      </div>
    </header>
  )
}

// --- Desktop right rail -----------------------------------------------------

/** Dispatch a nudge action to its overlay — mirrors the /activity screen. */
function useNudgeAction() {
  const { openSheet } = useSheet()
  const navigate = useNavigate()
  return (action: NudgeActionDto) => {
    switch (action.kind) {
      case 'viewEntry':
        openSheet('entry', { id: action.targetId })
        break
      case 'viewRecurring':
        navigate({ to: '/recurring', search: { sheet: 'recurring', id: action.targetId } })
        break
      case 'settle':
        openSheet('settle', { person: action.targetId })
        break
    }
  }
}

/**
 * Persistent desktop right rail (≥980px): upcoming auto-posts ("På gång") and
 * compact nudges ("Notiser"). Mirrors the canonical desktop layout's third
 * column; hidden on mobile where the same content lives on Home / the activity
 * route. Renders and calls only (ADR-0006).
 */
function RightRail() {
  const { householdId } = useActiveHousehold()
  const { openSheet } = useSheet()
  const runAction = useNudgeAction()
  const summary = useSummary(householdId)
  const nudges = useNudges(householdId, useNudgeTone())

  const upcoming = summary.data?.upcoming ?? []
  const nudgeList = nudges.data ?? []

  return (
    <aside className="sticky top-7 hidden h-fit flex-col gap-[22px] self-start py-8 pr-6 min-[980px]:flex">
      {upcoming.length > 0 && (
        <div>
          <p className={RAIL_LABEL}>På gång</p>
          <div className="mt-2.5 flex flex-col gap-2">
            {upcoming.map((item) => (
              <GhostCard
                key={item.recurringId}
                onClick={() => openSheet('recurring', { id: item.recurringId })}
                className="flex items-center gap-3 rounded-[14px] px-3.5 py-2.5"
              >
                <span
                  aria-hidden="true"
                  className="grid size-8 shrink-0 place-items-center rounded-[10px] bg-accent text-sm font-bold text-accent-foreground"
                >
                  ↻
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-[13px] font-semibold">{item.title}</span>
                  <span className="block truncate text-[11px] font-semibold text-accent-foreground">
                    Bokförs {shortDate(item.nextPostDate)} · {inDays(item.nextPostDate)}
                  </span>
                </span>
                <span className="shrink-0 text-right">
                  <Money minor={item.yourShareMinor} className="block text-xs font-semibold" />
                  <span className="block text-[10px] text-muted-foreground">din del</span>
                </span>
              </GhostCard>
            ))}
          </div>
        </div>
      )}

      <div>
        <p className={RAIL_LABEL}>Notiser</p>
        <div className="mt-2.5 flex flex-col gap-2">
          {nudgeList.length > 0 ? (
            nudgeList.map((nudge, i) => (
              <RailNudgeCard key={i} nudge={nudge} onAction={runAction} />
            ))
          ) : (
            <p className="rounded-[14px] border-[1.5px] border-dashed border-border p-4 text-xs leading-relaxed text-muted-foreground">
              Allt lugnt. Du hör av oss vid en stor utgift, ett saldo som blir stort eller en
              återkommande kostnad som snart bokförs.
            </p>
          )}
        </div>
      </div>
    </aside>
  )
}

function RailNudgeCard({
  nudge,
  onAction,
}: {
  nudge: NudgeDto
  onAction: (action: NudgeActionDto) => void
}) {
  return (
    <div className="rounded-2xl border border-border bg-card p-3">
      <div className="flex items-start gap-2.5">
        <span aria-hidden="true" className="mt-1.5 size-[7px] flex-none rounded-full bg-primary" />
        <div className="min-w-0 flex-1">
          <p className="text-[13px] font-[650]">{nudge.title}</p>
          <p className="mt-0.5 text-[11.5px] leading-snug text-muted-foreground">{nudge.body}</p>
          {nudge.actions.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {nudge.actions.map((action, i) => (
                <Button
                  key={i}
                  variant="secondary"
                  size="sm"
                  className="h-7 rounded-full px-3 text-[11.5px]"
                  onClick={() => onAction(action)}
                >
                  {action.label}
                </Button>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// --- Mobile bottom tab bar --------------------------------------------------

function MobileTabBar() {
  const { openSheet } = useSheet()

  return (
    <nav className="grid grid-cols-[1fr_1fr_66px_1fr_1fr] items-center border-t border-border bg-background/95 px-2 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] backdrop-blur min-[980px]:hidden">
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
