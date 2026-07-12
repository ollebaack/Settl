# ADR-0002: Frontend is a Vite SPA with TanStack Router/Query and shadcn (Luma, Base UI)

- **Status:** accepted
- **Date:** 2026-07-12

## Context

Settl is an app-like product behind a login; SEO and SSR buy nothing. The long-term goal
is a native iOS app via Expo, so the web UI layer is expected to be rebuilt, not reused.

## Decision

We will build the web app as a Vite SPA: React 19, TanStack Router (file-based) for
routing, TanStack Query for server state, Tailwind v4 + shadcn in the Luma style on
Base UI primitives. No SSR framework.

## Consequences

Simplest possible frontend architecture and the best-documented one. shadcn components
are DOM-only and will not carry to Expo — acceptable because ADR-0006 keeps all business
logic out of the UI. Switching shadcn styles later rewrites `components/ui/` files and
loses manual customizations made there.
