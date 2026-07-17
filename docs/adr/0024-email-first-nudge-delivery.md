# ADR-0024: Email-first nudge delivery, defer web/native push

- **Status:** accepted
- **Date:** 2026-07-17

## Context

The product brief's headline differentiator — "smart, event-triggered reminders" —
is display-only today: nudges are derived on read (ADR-0007) and rendered in-app, but
never leave the app. Turning them into real reminders forces a channel decision now,
and the channel constrains everything downstream (scheduler, consent, infra). The
obvious "push notifications" answer does not survive contact with the target market:
Settl is a Swedish/SEK/EU product, and iOS web push is unavailable in the EU under
Apple's DMA changes (iOS 17.4+) — even elsewhere it requires the user to install the
PWA to the home screen. Native push (Expo/APNs/FCM) would reach iPhones but depends on
the native app the brief lists as a *long-term* goal plus a paid Apple Developer
account. Resend is already our email vendor (ADR-0011) and supports scheduled sends,
batch, and idempotency keys.

## Decision

We will deliver nudges by **email via Resend** for v1, and defer both web push and
native push. Email is the only channel that reaches every user — including iPhone
owners in the EU — with zero install friction, and it reuses the Resend integration
already in place. Push is revisited when the native Expo app is actually built, not
before.

## Consequences

iPhone users get no real-time push in v1 — email is the whole delivery story until the
native app ships, at which point native push (not EU-crippled web push) becomes the
path and this ADR is revisited. Email is inherently less immediate than push, which is
acceptable given the deliberate anti-nag cadence (a daily digest, see the reminder
delivery spec) — timeliness was never the goal, "only speaks up when something happens"
is. Delivery leans harder on Resend, deepening a single-vendor dependency (ADR-0011);
its free tier still covers Settl's volume. We take on a scheduler and a persisted,
de-duplicated **emitted-nudge log** we didn't have before (nudges were storage-free on
read) — this is exactly the record ADR-0023 and tech-debt/0002 deferred until real
out-of-app delivery exists; this feature pays that debt down. The reminder delivery spec
owns that design. Note there is no shared "crossing-state" store to reuse: ADR-0023's
balance-crossing detection is derived on read (7-day window), so the emitted-nudge log
keys off each nudge's own derivable identity, not a mutable state table. Revisit when:
the native app ships (push becomes viable), or Resend volume/limits stop fitting the
free tier.

## Sources

- ADR-0007: Ledger data model (nudges derived on read, no storage)
- ADR-0023: Balance nudge crossing detection — derives crossings on read and explicitly
  defers the persisted, de-duplicated emitted-nudge log to this out-of-app delivery work
- tech-debt/0002: Nudges derived, not persisted — pay-down trigger is exactly this feature
- ADR-0011: Resend chosen for email delivery (vendor already in place)
- product-brief.md: reminders as the headline differentiator; native iOS as long-term
- Apple, "Sending web push notifications in web apps and browsers" — home-screen/PWA
  requirement: https://developer.apple.com/documentation/usernotifications/sending-web-push-notifications-in-web-apps-and-browsers
- MagicBell, "PWA iOS Limitations and Safari Support [2026]" — web push unavailable in
  the EU under the DMA (iOS 17.4+): https://www.magicbell.com/blog/pwa-ios-limitations-safari-support-complete-guide
- Expo, "Push notifications overview" — native push needs EAS build + Apple Developer
  account: https://docs.expo.dev/push-notifications/overview/
- Resend email API — scheduled sends, batch, idempotency: https://resend.com/features/email-api
