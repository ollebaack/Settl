// Safety net: force-remove any throwaway Postgres container from THIS run that
// api-with-postgres.mjs left behind (e.g. if it was hard-killed on Windows without
// running its own cleanup). Scoped to the run's label so a concurrent e2e run in
// another worktree is never touched.
import { spawnSync } from 'node:child_process'

export default function globalTeardown() {
  const runId = process.env.E2E_RUN_ID
  if (!runId) return
  const found = spawnSync(
    'docker',
    ['ps', '-aq', '--filter', `label=settl-e2e-run=${runId}`],
    { encoding: 'utf8' },
  )
  const ids = (found.stdout ?? '').split('\n').map((s) => s.trim()).filter(Boolean)
  if (ids.length) spawnSync('docker', ['rm', '-f', ...ids], { stdio: 'ignore' })
}
