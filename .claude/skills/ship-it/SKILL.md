---
name: ship-it
description: Ship the current branch end-to-end — open a PR, watch CI, fix any CI failures, merge, watch the production deploy roll out, then clean up the worktree and local branch. Use when the user says "ship it", "/ship-it", "ship this", or otherwise asks to take the current changes from branch to production.
---

# Ship it

Takes the work on the current branch all the way to production. Runs as a loop:
**PR → watch CI → fix failures → merge → watch deploy → clean up**. Don't stop halfway —
carry it through to a rolled-out deploy and a tidied-up local state (or a clear blocker),
reporting progress at each step.

Uses the `gh` CLI throughout. CI is `.github/workflows/ci.yml` (jobs `api`, `web`,
`e2e`); the production deploy is `.github/workflows/cd.yml`, which runs *after* a green
CI on `main` and rolls out via Dokploy (see [production.md](../../../docs/ops/production.md)
"Deploy flow").

## 0. Preflight

- Run `pnpm verify` first (CLAUDE.md: it must pass before work is done). Fixing it here
  is cheaper than a red CI run later. If it fails, fix and re-run before opening the PR.
- Confirm you're not on `main`. If you are, branch first (`git switch -c <name>`) — never
  push straight to `main`.
- Ensure everything is committed and the branch is pushed
  (`git push -u origin HEAD`). If an API-shape change touched
  `packages/api-client`, confirm it was regenerated in this branch (CLAUDE.md rule).

## 1. Open the PR

- `gh pr create` with a title matching the repo's Conventional-Commits style (see recent
  `git log` — e.g. `feat(api): ...`, `fix(e2e): ...`) and a body summarizing what changed
  and why. Reference the relevant ADR / tech-debt entry if one exists.
- End the PR body with the standard trailer:
  `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- If a PR for this branch already exists, reuse it (`gh pr view`) instead of erroring.

## 2. Watch CI

- `gh pr checks --watch` (or `gh run watch <run-id>`) until all checks settle.
- CI runs on every PR: `api` (dotnet build + test), `web` (lint + build), `e2e`
  (Playwright against a Postgres service). All three must be green.

## 3. Fix CI failures (if any)

- Inspect the failing job: `gh run view <run-id> --log-failed`.
- Reproduce locally where you can — `dotnet test`, `pnpm --filter web lint`,
  `pnpm --filter web build`, or `pnpm e2e` — so the fix is verified before you push.
- For flaky e2e failures, check the memory notes on the dev verification-link race and
  the Aspire Postgres port before assuming a real regression.
- Commit the fix, push, and go back to **step 2**. Loop until CI is green. Don't merge a
  red or still-running PR.

## 4. Merge

- Merge once CI is fully green: `gh pr merge --squash --delete-branch`. Squash matches
  the repo's history (recent commits are squashed `... (#NN)`).
- **A non-zero exit is not automatically a merge failure — verify before reacting.** In
  this repo's multi-worktree setup, `--delete-branch` makes `gh` try to check out `main`
  locally after merging, which fails with `fatal: 'main' is already used by worktree at
  ...` because the main worktree holds it. The *remote* merge and branch deletion still
  succeeded; only the local post-merge checkout failed. So whenever the command exits
  non-zero, confirm the real state with
  `gh pr view <n> --json state,mergeCommit` before deciding anything:
  - `state: "MERGED"` → the merge went through; capture the `mergeCommit.oid` and continue
    to step 5. The local-checkout error is cosmetic (the local branch cleanup just moves to
    step 6 instead).
  - `state: "OPEN"` and it's a genuine block (required reviews, branch protection,
    conflicts) → stop and tell the user exactly what's blocking. Don't try to force it.

## 5. Watch the deploy

- The merge to `main` triggers CI again on `main`; **only after that CI passes** does CD
  run (`cd.yml` keys off `workflow_run: [CI] completed` on `main`). Watch both:
  `gh run watch` the `main` CI run, then the CD run.
- CD builds the image, pushes it to GHCR (`ghcr.io/ollebaack/settl-api`), and calls the
  Dokploy deploy API. Watch it to green: `gh run view <cd-run-id> --log`. The final
  "Trigger Dokploy deploy" step returning 200 means Dokploy accepted the rollout.
- Confirm the rollout actually landed rather than assuming: hit the live site
  (`curl -fsS -o /dev/null -w '%{http_code}' https://settlapp.se`) and report the result.
  If it looks like the new image wasn't picked up, point the user at the Dokploy
  deployment log / manual **Deploy** button (production.md "Deploy flow" step 4) —
  Dokploy itself is outside `gh`'s reach.

## 6. Clean up

Only once the deploy is confirmed and the PR is merged — never before. The remote branch
is already gone (`--delete-branch` in step 4); this tidies the local side.

**Never hand the user manual cleanup steps.** Do only what you can run cleanly right now,
from wherever this session is sitting; anything you can't do from here is handled
automatically when the session ends. Do **not** print a "manual cleanups left" list, a
`git worktree remove` / `rm -rf` command for the user to run, or a Docker/Postgres teardown
suggestion. If a cleanup can't run from inside the worktree, that is expected — say nothing
about it rather than turning it into a task for the user.

- **Worktree:** if it was created by `EnterWorktree` earlier in *this* session, exit it with
  the `ExitWorktree` tool, `action: "remove"` — it restores the original directory and
  deletes the worktree + branch in one step. It refuses if there are uncommitted changes or
  unmerged commits; since everything just shipped there should be none, but if it lists any,
  stop and confirm with the user rather than passing `discard_changes: true` blindly.
  Otherwise (the session merely started inside a pre-existing worktree, so `ExitWorktree` is
  a no-op) just leave it — the cwd is anchored here so it can't be removed from inside, and
  the harness prompts to remove it when the session ends. Don't attempt it, don't mention it.
- **Prune what runs cleanly from here:** `git worktree prune` and `git fetch --prune` work
  from inside the worktree and are safe — run them to drop stale metadata and the deleted
  remote-tracking ref. Skip `git branch -d <branch>` while that branch is the one checked out
  by this worktree (it can't be deleted from here and clears on session exit); only run it if
  the shipped branch is a separate local branch not tied to the live worktree.

## Report

Finish with a **short, scannable summary** the user can read at a glance — not a wall of
prose. Lead with a status emoji and a one-line headline of what shipped, then a few
compact lines for the essentials. Keep it tight: this is a "what just happened" glance,
not a log.

On success, use `🚀` and cover, one short line each: what shipped (the PR title/number),
CI result, the merge commit, the deploy result + live-site status code, and cleanup (what
was done). Report cleanup as done — the worktree folder clearing on session exit is normal
housekeeping, not a caveat, so don't surface it as a leftover task or a manual command. For
example:

> 🚀 **Shipped #38** — ship-it verifies PR state on non-zero merge exit
> - **CI:** api · web · e2e all green
> - **Merged:** `aaa4b62` (squash)
> - **Deploy:** CD green, `https://settlapp.se` → 200 ✅
> - **Cleanup:** remote branch + ref pruned · worktree clears on session exit

If you stopped at a blocker (red CI you couldn't fix, protected merge, deploy that didn't
roll out), lead with `🛑` instead, name the exact failing step in the headline, and say
what's needed to unblock. Cleanup is never a blocker and never a manual step — leftover
local state clears on session exit, so don't report it. Add a `⚠️` line for
anything shipped-but-noteworthy (e.g. a deploy SHA that differs because a later merge
superseded it). Put full URLs and any longer detail below the summary if the user needs
them — keep the headline block clean.
