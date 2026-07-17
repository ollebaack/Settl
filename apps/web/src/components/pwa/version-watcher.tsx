/**
 * Update watcher (ADR-0027). With no service worker, a warm (never-closed)
 * home-screen session keeps running the old client bundle until the page
 * reloads — and standalone mode hides the browser's reload button. This polls
 * the deployed build id and offers a one-tap refresh when it moves on. Data
 * itself is always live via TanStack Query; only the bundle can go stale.
 *
 * Dev is skipped: version.json is emitted only by the production build.
 */
import { useEffect, useRef } from 'react'
import { toast } from 'sonner'
import { APP_VERSION } from '@/lib/pwa'

const POLL_MS = 60_000

export function VersionWatcher() {
  const notified = useRef(false)

  useEffect(() => {
    if (!import.meta.env.PROD) return
    let cancelled = false

    async function check() {
      if (cancelled || notified.current || document.hidden) return
      try {
        const res = await fetch('/version.json', { cache: 'no-store' })
        if (!res.ok) return
        const { version } = (await res.json()) as { version?: string }
        if (!version || version === APP_VERSION) return
        notified.current = true
        toast('En ny version av Settl finns', {
          description: 'Ladda om för att uppdatera.',
          duration: Infinity,
          action: { label: 'Ladda om', onClick: () => window.location.reload() },
        })
      } catch {
        // Offline or transient — retry on the next tick / focus.
      }
    }

    const id = window.setInterval(check, POLL_MS)
    const onVisible = () => {
      if (!document.hidden) void check()
    }
    document.addEventListener('visibilitychange', onVisible)
    void check()

    return () => {
      cancelled = true
      window.clearInterval(id)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [])

  return null
}
