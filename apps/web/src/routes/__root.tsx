import { createRootRoute, Outlet, useLocation } from '@tanstack/react-router'
import { AppShell } from '@/components/app-shell'
import { DevBar } from '@/components/dev-bar'
import { SheetRouter } from '@/components/sheet-router'
import { Toaster } from '@/components/toaster'
import { InstallSheet } from '@/components/pwa/install-sheet'
import { VersionWatcher } from '@/components/pwa/version-watcher'
import { validateSheetSearch, type SheetSearch } from '@/lib/sheet'

export const Route = createRootRoute({
  // Sheet overlays are driven by URL search params (?sheet=…&id=…&person=…).
  validateSearch: (search: Record<string, unknown>): SheetSearch =>
    validateSheetSearch(search),
  component: RootLayout,
})

// Auth pages (ADR-0011) render without the app chrome — there's no household nav
// to show before/without a logged-in user.
const AUTH_ROUTES = [
  '/login',
  '/signup',
  '/accept-invite',
  '/verify-email',
  '/confirm-email',
  '/reset-password',
]

function RootLayout() {
  const { pathname } = useLocation()

  if (AUTH_ROUTES.includes(pathname)) {
    return (
      <>
        <Outlet />
        <Toaster />
        <VersionWatcher />
        <div className="fixed inset-x-0 bottom-0 z-40">
          <DevBar />
        </div>
      </>
    )
  }

  return (
    <>
      <AppShell>
        <Outlet />
      </AppShell>
      <SheetRouter />
      <Toaster />
      {/* Manifest-only PWA (ADR-0027): iOS install guidance + no-service-worker update watcher. */}
      <InstallSheet />
      <VersionWatcher />
    </>
  )
}
