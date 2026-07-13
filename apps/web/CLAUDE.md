# Web rules

- **Check `docs/design/` before building or changing a screen.** `implementation-map.md`
  is the synthesized build spec (routes, per-screen components, copy, states,
  formatting rules) — start there. It also gives source precedence across the
  raw design exports (`Settl Prototype.dc.html` for UI structure/interactions,
  `Settl App`/`Settl Web.dc.html` for Swedish copy, `Settl Shadcn Handoff.dc.html`
  for theme tokens/component mapping) plus the addenda files. Don't invent
  screen structure, copy, or states that contradict it.
- **shadcn first**: if a shadcn component fits, use it — never hand-roll one.
  Install missing: `pnpm dlx shadcn@latest add <name>`. Custom components are
  domain UI only, composed from shadcn primitives.
- Never edit `components/ui/` (rewritten on style switch); customize via wrappers
  and `className`.
- Tailwind semantic tokens only (`bg-background`, `text-muted-foreground`, …) —
  no hex values, no CSS files. Conditional classes via `cn()`.
- Server state via TanStack Query + `apiFetch` (`@/lib/api`); never fetch in
  `useEffect`. Routes are file-based in `src/routes/`.
- API types come from `@settl/api-client` — never hand-write them.
- Surface errors to the user; no silent catch, no infinite spinners.
- Mobile-first; root layout is `max-w-md` on purpose.
- Copy tone: shared household notebook — never nagging, never fintech.
- Testing is e2e, not unit: every user flow gets a Playwright spec in `e2e/`
  (add/edit entry, settle, recurring pause/resume, switch household, …).
  `pnpm e2e` from root runs against the real API (isolated `e2e.db`). Run it
  before UI work is done. Specs assert user-visible behavior, not markup details.
