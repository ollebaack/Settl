/**
 * PWA helpers (ADR-0027). Settl ships as a manifest-only installable web app —
 * no service worker. iOS has no `beforeinstallprompt`, so installing there is a
 * manual Share → Add to Home Screen; these helpers decide when to guide the user
 * and expose the running build id for the update watcher.
 */

/** The build id baked in at build time (see vite.config.ts). */
export const APP_VERSION = typeof __APP_VERSION__ === 'string' ? __APP_VERSION__ : 'dev'

/** `true` when running as an installed, standalone home-screen app (not a browser tab). */
export function isStandalone(): boolean {
  if (typeof window === 'undefined') return false
  return (
    window.matchMedia?.('(display-mode: standalone)').matches === true ||
    // iOS Safari exposes this non-standard flag instead of the display-mode query.
    (window.navigator as Navigator & { standalone?: boolean }).standalone === true
  )
}

/** `true` on iOS / iPadOS (all iOS browsers are WebKit under the hood). */
export function isIos(): boolean {
  if (typeof navigator === 'undefined') return false
  const ua = navigator.userAgent
  return (
    /iPad|iPhone|iPod/.test(ua) ||
    // iPadOS 13+ reports as "MacIntel"; disambiguate by touch support.
    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
  )
}

/**
 * `true` only where Add to Home Screen actually works: iOS in the real Safari.
 * Chrome (CriOS), Firefox (FxiOS), Edge (EdgiOS), the Google app (GSA) and other
 * in-app WebViews on iOS cannot install, so we must not show them Safari's steps.
 */
export function isIosSafari(): boolean {
  if (!isIos()) return false
  return !/CriOS|FxiOS|EdgiOS|GSA|Instagram|FBAN|FBAV|Line/i.test(navigator.userAgent)
}

/** Whether we can meaningfully guide this user to install (iOS Safari, not yet installed). */
export function canGuideInstall(): boolean {
  return isIosSafari() && !isStandalone()
}
