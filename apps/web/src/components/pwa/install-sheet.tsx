/**
 * iOS "Add to Home Screen" guidance (installable-pwa spec). iOS has no install prompt API,
 * and the Share → Add to Home Screen step is hidden, so we spell it out. Shown
 * only where it can work (Safari on iOS, not already installed): auto-opens once
 * per device, and is reachable any time from the account menu via
 * `openInstallSheet()`. Android/desktop use the browser's own install affordance
 * and never see this.
 */
import { useEffect } from 'react'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Button } from '@/components/ui/button'
import { canGuideInstall } from '@/lib/pwa'
import { setInstallSheetOpen, useInstallSheetOpen } from '@/lib/install-prompt'

const DISMISS_KEY = 'settl.installGuideDismissed'

function wasDismissed() {
  try {
    return localStorage.getItem(DISMISS_KEY) === '1'
  } catch {
    return false
  }
}
function markDismissed() {
  try {
    localStorage.setItem(DISMISS_KEY, '1')
  } catch {
    /* private mode — fine, we just re-prompt next session */
  }
}

/** The iOS Share icon (square with an upward arrow) — drawn to match Safari. */
function ShareGlyph() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="size-5 text-primary">
      <path
        d="M12 3v11M12 3 8.5 6.5M12 3l3.5 3.5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M7 10H5.5A1.5 1.5 0 0 0 4 11.5v7A1.5 1.5 0 0 0 5.5 20h13a1.5 1.5 0 0 0 1.5-1.5v-7A1.5 1.5 0 0 0 18.5 10H17"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

/** A square-with-plus, matching iOS's "Add to Home Screen" row icon. */
function AddGlyph() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="size-5 text-primary">
      <rect x="4" y="4" width="16" height="16" rx="4" fill="none" stroke="currentColor" strokeWidth="1.8" />
      <path d="M12 9v6M9 12h6" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  )
}

function Step({ n, glyph, children }: { n: number; glyph?: React.ReactNode; children: React.ReactNode }) {
  return (
    <li className="flex items-center gap-3 rounded-2xl border border-border bg-card p-3">
      <span className="grid size-7 shrink-0 place-items-center rounded-full bg-accent text-[13px] font-bold text-accent-foreground">
        {n}
      </span>
      <span className="flex-1 text-[13.5px] leading-snug text-foreground">{children}</span>
      {glyph && <span className="shrink-0">{glyph}</span>}
    </li>
  )
}

export function InstallSheet() {
  const open = useInstallSheetOpen()

  // One-time auto-prompt: only on iOS Safari, not already installed, not dismissed.
  useEffect(() => {
    if (open || !canGuideInstall() || wasDismissed()) return
    const id = window.setTimeout(() => setInstallSheetOpen(true), 2500)
    return () => window.clearTimeout(id)
  }, [open])

  function onOpenChange(next: boolean) {
    if (!next) markDismissed() // seen it — don't auto-prompt again (menu still reopens it)
    setInstallSheetOpen(next)
  }

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={onOpenChange}
      title="Lägg till Settl på hemskärmen"
      description="Öppna Settl som en app — snabbare start, egen ikon och helskärm. Så här gör du i Safari:"
    >
      <ol className="mt-1 flex flex-col gap-2">
        <Step n={1} glyph={<ShareGlyph />}>
          Tryck på <span className="font-semibold">Dela</span> i verktygsfältet.
        </Step>
        <Step n={2} glyph={<AddGlyph />}>
          Välj <span className="font-semibold">Lägg till på hemskärmen</span>.
        </Step>
        <Step n={3}>
          Tryck <span className="font-semibold">Lägg till</span> — klart. Settl hamnar bland dina appar.
        </Step>
      </ol>
      <Button variant="ghost" className="mt-3 w-full" onClick={() => onOpenChange(false)}>
        Inte nu
      </Button>
    </ResponsiveSheet>
  )
}
