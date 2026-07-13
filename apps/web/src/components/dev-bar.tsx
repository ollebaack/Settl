/**
 * DEV-only diagnostics bar (docs/specs/dev-bar.md). A thin bar docked to the
 * bottom of the viewport with inline action buttons — Console / Requests
 * toggle an in-flow panel directly above the bar (no overlay/Drawer), Copy
 * markdown snapshots debug context to the clipboard, Clear resets both
 * buffers. Never rendered in a production build — the `import.meta.env.DEV`
 * guard below is the only gate (no feature flag).
 */
import { useState, useSyncExternalStore } from 'react'
import { useLocation } from '@tanstack/react-router'
import { ClipboardIcon, Trash2Icon, TerminalIcon, XIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  consoleErrorsStore,
  requestsStore,
  type ConsoleErrorEntry,
  type RequestEntry,
} from '@/lib/dev-bar-store'
import { useMe } from '@/lib/queries'
import { useActiveHousehold } from '@/lib/active-household'
import { cn } from '@/lib/utils'

export function DevBar() {
  if (!import.meta.env.DEV) return null
  return <DevBarInner />
}

type Panel = 'console' | 'requests' | null

function time(iso: string): string {
  return new Date(iso).toLocaleTimeString('sv-SE')
}

function isErrorStatus(status: RequestEntry['status']): boolean {
  return status === 'error' || status >= 400
}

function buildMarkdown(args: {
  memberId: string | undefined
  memberName: string | undefined
  householdId: string | undefined
  householdName: string | undefined
  pathname: string
  errors: ConsoleErrorEntry[]
  requests: RequestEntry[]
}): string {
  const { memberId, memberName, householdId, householdName, pathname, errors, requests } = args
  const capturedAt = new Date().toISOString()

  const lines = [
    `## Dev snapshot — ${capturedAt}`,
    '',
    `- User: ${memberName ?? 'unknown'}${memberId ? ` (${memberId})` : ''}`,
    `- Household: ${householdName ?? 'unknown'}${householdId ? ` (${householdId})` : ''}`,
    `- Route: ${pathname}`,
    '',
    `### Console errors (${errors.length})`,
  ]

  if (errors.length === 0) {
    lines.push('- none')
  } else {
    for (const e of errors) {
      lines.push(`- ${time(e.timestamp)} — ${e.message}`)
      if (e.stack) lines.push(`  ${e.stack.split('\n').join('\n  ')}`)
    }
  }

  lines.push('', `### Requests (${requests.length})`)
  if (requests.length === 0) {
    lines.push('- none')
  } else {
    for (const r of requests) {
      lines.push(`- ${time(r.timestamp)} · ${r.method} ${r.path} · ${r.status} · ${r.durationMs}ms`)
    }
  }

  return lines.join('\n')
}

function DevBarInner() {
  const [panel, setPanel] = useState<Panel>(null)
  const errors = useSyncExternalStore(consoleErrorsStore.subscribe, consoleErrorsStore.getSnapshot)
  const requests = useSyncExternalStore(requestsStore.subscribe, requestsStore.getSnapshot)
  const { data: me } = useMe()
  const { household, householdId } = useActiveHousehold()
  const { pathname } = useLocation()

  const lastRequest = requests[requests.length - 1]
  const failedRequestCount = requests.filter((r) => isErrorStatus(r.status)).length

  function togglePanel(next: Exclude<Panel, null>) {
    setPanel((current) => (current === next ? null : next))
  }

  async function copyToMarkdown() {
    const markdown = buildMarkdown({
      memberId: me?.id,
      memberName: me?.name,
      householdId,
      householdName: household?.name,
      pathname,
      errors,
      requests,
    })
    try {
      await navigator.clipboard.writeText(markdown)
      toast('Copied dev snapshot to clipboard')
    } catch {
      toast('Could not copy to clipboard')
    }
  }

  function clear() {
    consoleErrorsStore.clear()
    requestsStore.clear()
    setPanel(null)
  }

  return (
    <div className="flex flex-col border-t border-border bg-background/95 backdrop-blur">
      {panel && (
        <div className="flex items-center justify-between border-b border-border px-3 py-1.5">
          <span className="text-xs font-medium text-foreground">
            {panel === 'console' ? 'Console errors' : 'Requests'}
          </span>
          <Button variant="ghost" size="icon" className="size-6" onClick={() => setPanel(null)}>
            <XIcon className="size-3.5" />
          </Button>
        </div>
      )}

      {panel === 'console' && (
        <ScrollArea className="h-[35dvh]">
          <div className="flex flex-col gap-2 p-3">
            {errors.length === 0 ? (
              <EmptyState label="No uncaught errors yet." />
            ) : (
              [...errors].reverse().map((e, i) => <ConsoleErrorRow key={i} entry={e} />)
            )}
          </div>
        </ScrollArea>
      )}

      {panel === 'requests' && (
        <ScrollArea className="h-[35dvh]">
          <div className="flex flex-col gap-1 p-3">
            {requests.length === 0 ? (
              <EmptyState label="No requests yet." />
            ) : (
              [...requests].reverse().map((r, i) => <RequestRow key={i} entry={r} />)
            )}
          </div>
        </ScrollArea>
      )}

      <div className="flex w-full items-center gap-1.5 overflow-x-auto px-2 py-1.5 text-xs">
        <TerminalIcon className="size-3.5 shrink-0 text-muted-foreground" />
        <span className="shrink-0 font-medium text-foreground">Dev</span>

        <Button
          variant={panel === 'console' ? 'secondary' : 'ghost'}
          size="sm"
          className={cn(
            'h-6 shrink-0 gap-1 px-2 text-xs',
            errors.length > 0 && 'text-destructive hover:text-destructive',
          )}
          onClick={() => togglePanel('console')}
        >
          Console
          {errors.length > 0 && <Badge variant="destructive">{errors.length}</Badge>}
        </Button>

        <Button
          variant={panel === 'requests' ? 'secondary' : 'ghost'}
          size="sm"
          className={cn(
            'h-6 shrink-0 gap-1 px-2 text-xs',
            failedRequestCount > 0 && 'text-destructive hover:text-destructive',
          )}
          onClick={() => togglePanel('requests')}
        >
          Requests ({requests.length})
          {failedRequestCount > 0 && <Badge variant="destructive">{failedRequestCount}</Badge>}
        </Button>

        {lastRequest && (
          <span className="min-w-0 flex-1 truncate font-mono text-muted-foreground">
            {lastRequest.method} {lastRequest.path} ·{' '}
            <span className={cn(isErrorStatus(lastRequest.status) && 'text-destructive')}>
              {lastRequest.status}
            </span>{' '}
            · {lastRequest.durationMs}ms
          </span>
        )}

        <div className="ml-auto flex shrink-0 items-center gap-1">
          <Button variant="ghost" size="sm" className="h-6 gap-1 px-2 text-xs" onClick={copyToMarkdown}>
            <ClipboardIcon className="size-3.5" />
            <span className="hidden sm:inline">Copy markdown</span>
          </Button>
          <Button variant="ghost" size="sm" className="h-6 gap-1 px-2 text-xs" onClick={clear}>
            <Trash2Icon className="size-3.5" />
            <span className="hidden sm:inline">Clear</span>
          </Button>
        </div>
      </div>
    </div>
  )
}

function EmptyState({ label }: { label: string }) {
  return <p className="py-6 text-center text-xs text-muted-foreground">{label}</p>
}

function ConsoleErrorRow({ entry }: { entry: ConsoleErrorEntry }) {
  return (
    <div className="rounded-lg border border-destructive/20 bg-destructive/5 p-2.5 font-mono text-xs">
      <div className="flex items-baseline gap-2">
        <span className="shrink-0 text-muted-foreground">{time(entry.timestamp)}</span>
        <span className="text-destructive">{entry.message}</span>
      </div>
      {entry.stack && (
        <pre className="mt-1 overflow-x-auto whitespace-pre-wrap text-muted-foreground">{entry.stack}</pre>
      )}
    </div>
  )
}

function RequestRow({ entry }: { entry: RequestEntry }) {
  const failed = isErrorStatus(entry.status)
  return (
    <div
      className={cn(
        'flex items-center gap-2 rounded-lg px-2.5 py-1.5 font-mono text-xs',
        failed && 'border border-destructive/20 bg-destructive/5 text-destructive',
      )}
    >
      <span className={cn('shrink-0', failed ? 'text-destructive/70' : 'text-muted-foreground')}>
        {time(entry.timestamp)}
      </span>
      <span className="shrink-0 font-semibold">{entry.method}</span>
      <span className="min-w-0 flex-1 truncate">{entry.path}</span>
      <span className="shrink-0">{entry.status}</span>
      <span className={cn('shrink-0', failed ? 'text-destructive/70' : 'text-muted-foreground')}>
        {entry.durationMs}ms
      </span>
    </div>
  )
}
