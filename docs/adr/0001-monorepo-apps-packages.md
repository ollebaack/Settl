# ADR-0001: Monorepo with apps/ and packages/

- **Status:** accepted
- **Date:** 2026-07-12

## Context

Settl will grow to three consumers of one API: web (now), Expo mobile (later), and a
shared generated API client. Solo developer; every feature should be one PR.

## Decision

We will use a single repo: `apps/web`, `apps/api`, later `apps/mobile`, with shared
TypeScript packages under `packages/`. pnpm workspaces for JS, one `.slnx` at root for .NET.

## Consequences

Contract, server, and client change together in one PR. Slightly more root-level tooling
(workspace config, mixed-language CI). Separate deployability is unaffected — apps are
independent build outputs.
