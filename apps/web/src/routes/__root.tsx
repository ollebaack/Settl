import { createRootRoute, Outlet } from '@tanstack/react-router'
import { AppShell } from '@/components/app-shell'
import { SheetRouter } from '@/components/sheet-router'
import { Toaster } from '@/components/toaster'
import { validateSheetSearch, type SheetSearch } from '@/lib/sheet'

export const Route = createRootRoute({
  // Sheet overlays are driven by URL search params (?sheet=…&id=…&person=…).
  validateSearch: (search: Record<string, unknown>): SheetSearch =>
    validateSheetSearch(search),
  component: RootLayout,
})

function RootLayout() {
  return (
    <>
      <AppShell>
        <Outlet />
      </AppShell>
      <SheetRouter />
      <Toaster />
    </>
  )
}
