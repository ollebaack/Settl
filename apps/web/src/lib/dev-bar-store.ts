/**
 * DEV-only diagnostics ring buffers for the dev bar (docs/specs/dev-bar.md).
 * External-store pattern (see stores.ts), in-memory only — resets on hard
 * reload. Listeners attach here, at module load, so capture starts at app
 * boot rather than lazily when the panel first opens; this module is pulled
 * in unconditionally via api.ts, but everything inside stays inert outside
 * `import.meta.env.DEV`.
 */

type Listener = () => void

export interface ConsoleErrorEntry {
  message: string
  stack?: string
  timestamp: string
}

export interface RequestEntry {
  method: string
  path: string
  status: number | 'error'
  durationMs: number
  timestamp: string
}

const MAX_ENTRIES = 20

function createRingBuffer<T>() {
  let entries: T[] = []
  const listeners = new Set<Listener>()
  const notify = () => listeners.forEach((l) => l())

  return {
    getSnapshot: () => entries,
    push: (entry: T) => {
      entries = [...entries, entry].slice(-MAX_ENTRIES)
      notify()
    },
    clear: () => {
      entries = []
      notify()
    },
    subscribe: (listener: Listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
  }
}

export const consoleErrorsStore = createRingBuffer<ConsoleErrorEntry>()
export const requestsStore = createRingBuffer<RequestEntry>()

export function recordRequest(entry: RequestEntry) {
  if (!import.meta.env.DEV) return
  requestsStore.push(entry)
}

function stringifyArg(arg: unknown): string {
  if (typeof arg === 'string') return arg
  if (arg instanceof Error) return arg.message
  try {
    return JSON.stringify(arg)
  } catch {
    return String(arg)
  }
}

if (import.meta.env.DEV) {
  window.addEventListener('error', (event) => {
    consoleErrorsStore.push({
      message: event.error instanceof Error ? event.error.message : event.message,
      stack: event.error instanceof Error ? event.error.stack : undefined,
      timestamp: new Date().toISOString(),
    })
  })

  window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason
    consoleErrorsStore.push({
      message: reason instanceof Error ? reason.message : String(reason),
      stack: reason instanceof Error ? reason.stack : undefined,
      timestamp: new Date().toISOString(),
    })
  })

  const originalConsoleError = console.error.bind(console)
  console.error = (...args: unknown[]) => {
    originalConsoleError(...args)
    const errorArg = args.find((a): a is Error => a instanceof Error)
    consoleErrorsStore.push({
      message: args.map(stringifyArg).join(' '),
      stack: errorArg?.stack,
      timestamp: new Date().toISOString(),
    })
  }
}
