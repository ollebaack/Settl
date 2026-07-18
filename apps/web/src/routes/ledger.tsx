/**
 * `/ledger` — retired (adaptive-home spec). The standalone Loggbok tab was folded into the
 * merged Hushållet page, so this route only redirects old deep links (and the
 * former "Visa alla" link) to `/hushallet`, where the log now lives.
 */
import { createFileRoute, redirect } from '@tanstack/react-router'

export const Route = createFileRoute('/ledger')({
  beforeLoad: () => {
    throw redirect({ to: '/hushallet' })
  },
})
