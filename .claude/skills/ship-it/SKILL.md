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
- If merge is blocked (required reviews, branch protection, conflicts), stop and tell the
  user exactly what's blocking — don't try to force it.

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
is already gone (`--delete-branch` in step 4); this cleans up the local side so nothing
stale is left behind.

- **Worktree** (this session is running in one — `.claude/worktrees/<name>`, see the
  environment/CLAUDE.md):
  - If the worktree was created by `EnterWorktree` earlier in *this* session, exit it with
    the `ExitWorktree` tool, `action: "remove"` — it restores the original directory and
    deletes the worktree + branch in one step. It refuses if there are uncommitted changes
    or unmerged commits; since everything just shipped there should be none, but if it
    lists any, stop and confirm with the user rather than passing `discard_changes: true`
    blindly.
  - Otherwise (the session merely started inside a pre-existing worktree, so `ExitWorktree`
    is a no-op), you **cannot** remove the worktree you're standing in. Confirm it's clean
    (`git status` shows nothing to commit), then tell the user the exact command to run
    from the main checkout: `git worktree remove .claude/worktrees/<name>` followed by
    `git worktree prune`. Don't attempt it from inside the worktree.
- **Local branch:** if the shipped branch still exists locally after the worktree is gone,
  delete it: `git branch -d <branch>` (use `-d`, not `-D`, so a not-fully-merged branch is
  a signal to stop, not force-delete).
- **Stale leftovers:** `git worktree prune` (drops removed-worktree metadata) and
  `git fetch --prune` (drops the remote-tracking ref for the now-deleted remote branch).
  Skip anything already clean — don't invent work.

## Report

When done, give the user the PR URL, the merge commit, the CD run link, the live-site
check result, and what was cleaned up (or the exact command left for them to run if the
worktree couldn't be removed from inside itself). If you stopped at a blocker (red CI you
couldn't fix, protected merge, deploy that didn't roll out, uncommitted changes blocking
cleanup), say so plainly with the specific failing step.
