// Boots a throwaway Postgres container, waits until it's ready, then starts the Settl
// API pointed at it — all in ONE process so ordering is guaranteed. Playwright starts
// `webServer` commands *before* globalSetup and doesn't order webServers relative to each
// other, so the DB can't be a separate globalSetup/webServer step (the API's startup
// migration would race an unready DB). Wrapping DB-then-API in a single command is the
// documented way to express that dependency (microsoft/playwright#37237).
//
// Used as the API `webServer.command` in playwright.config.ts. The container is labelled
// per run and removed on exit; global-teardown.mjs sweeps strays from THIS run only, so
// parallel runs in other worktrees don't tear each other down.
//
// Env:
//   E2E_API_URL   API origin to bind (ASPNETCORE_URLS).      Default http://localhost:5000
//   E2E_DB        If set, skip the container and use this connection string as-is
//                 (CI's `services: postgres` and manual overrides take this path).
//   E2E_RUN_ID    Run scope for the cleanup label.           Default this process's pid.
//   PG_IMAGE      Postgres image.                            Default postgres:16 (matches CI).
import { spawn, spawnSync } from 'node:child_process'

const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5000'
const PG_IMAGE = process.env.PG_IMAGE ?? 'postgres:16'
const RUN_ID = process.env.E2E_RUN_ID ?? String(process.pid)

const docker = (args, opts = {}) => spawnSync('docker', args, { encoding: 'utf8', ...opts })

let containerName = null
let cleanedUp = false
function cleanup() {
  if (cleanedUp || !containerName) return
  cleanedUp = true
  docker(['rm', '-f', containerName], { stdio: 'ignore' })
}

let connectionString = process.env.E2E_DB

if (!connectionString) {
  containerName = `settl-e2e-pg-${RUN_ID}-${process.pid}`
  // Clean up signals early so a Ctrl-C during DB startup still removes the container.
  process.on('SIGINT', () => { cleanup(); process.exit(130) })
  process.on('SIGTERM', () => { cleanup(); process.exit(143) })
  process.on('exit', cleanup)

  console.log(`[e2e-db] starting ${PG_IMAGE} as ${containerName}`)
  // Let Docker pick a free host port (`-p 127.0.0.1::5432`) to avoid a find-a-port race.
  const up = docker([
    'run', '-d', '--rm',
    '--name', containerName,
    '--label', 'settl-e2e-postgres',
    '--label', `settl-e2e-run=${RUN_ID}`,
    '-e', 'POSTGRES_USER=postgres',
    '-e', 'POSTGRES_PASSWORD=postgres',
    '-e', 'POSTGRES_DB=e2e',
    '-p', '127.0.0.1::5432',
    PG_IMAGE,
  ])
  if (up.status !== 0) {
    console.error(`[e2e-db] docker run failed:\n${up.stderr || up.stdout}`)
    process.exit(1)
  }

  const mapping = docker(['port', containerName, '5432/tcp']).stdout ?? ''
  const hostPort = mapping.match(/:(\d+)\s*$/m)?.[1]
  if (!hostPort) {
    console.error(`[e2e-db] could not read mapped port from: ${mapping}`)
    cleanup()
    process.exit(1)
  }
  connectionString = `Host=localhost;Port=${hostPort};Database=e2e;Username=postgres;Password=postgres`

  // The postgres image only accepts TCP connections once its init (incl. creating the
  // `e2e` DB) has finished, so a passing pg_isready means the database is usable.
  const deadline = Date.now() + 60_000
  let ready = false
  while (Date.now() < deadline) {
    if (docker(['exec', containerName, 'pg_isready', '-U', 'postgres', '-d', 'e2e'], { stdio: 'ignore' }).status === 0) {
      ready = true
      break
    }
    await new Promise((resolve) => setTimeout(resolve, 500))
  }
  if (!ready) {
    console.error('[e2e-db] Postgres did not become ready within 60s')
    cleanup()
    process.exit(1)
  }
  console.log(`[e2e-db] Postgres ready on ${connectionString.replace(/Password=[^;]*/, 'Password=***')}`)
}

// Start the API pointed at the DB; inherit stdio so Playwright sees /health come up.
const api = spawn('dotnet', ['run', '--project', '../api/Settl.Api', '--no-launch-profile'], {
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: API_URL,
    ConnectionStrings__Settl: connectionString,
  },
})

api.on('exit', (code, signal) => {
  cleanup()
  process.exit(code ?? (signal ? 1 : 0))
})
