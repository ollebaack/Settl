/**
 * Generates the PWA raster icons from the brand mark (ADR-0027), using the
 * Playwright chromium already installed for e2e — no extra dependency. This is
 * a one-off asset step, NOT part of the build: run it only when the mark or the
 * icon set changes, then commit the PNGs in public/.
 *
 *   node scripts/generate-pwa-icons.mjs   (from apps/web)
 */
import { chromium } from '@playwright/test'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import fs from 'node:fs'

const here = path.dirname(fileURLToPath(import.meta.url))
const publicDir = path.resolve(here, '../public')

// Brand mark, mirrored from public/favicon.svg: green gradient + white ledger glyph.
const GRAD = `<linearGradient id="g" x1="256" y1="0" x2="256" y2="512" gradientUnits="userSpaceOnUse">
    <stop offset="0" stop-color="#3f9d72" /><stop offset="1" stop-color="#2e7d5b" />
  </linearGradient>`
const GLYPH = `<path d="M348 176 C348 128 164 128 164 208 C164 268 348 244 348 312 C348 392 164 392 164 340"
    fill="none" stroke="#ffffff" stroke-width="48" stroke-linecap="round" stroke-linejoin="round" />`

// Rounded tile with transparent corners — the standard "any"-purpose icons.
const rounded = `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <defs>${GRAD}</defs><rect width="512" height="512" rx="140" fill="url(#g)" />${GLYPH}</svg>`

// Full-bleed square — for the maskable icon (Android masks the corners) and the
// iOS apple-touch-icon (iOS rounds it and renders any transparency as black).
const bleed = `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <defs>${GRAD}</defs><rect width="512" height="512" fill="url(#g)" />${GLYPH}</svg>`

const targets = [
  { svg: rounded, size: 192, file: 'icon-192.png', bg: true },
  { svg: rounded, size: 512, file: 'icon-512.png', bg: true },
  { svg: bleed, size: 512, file: 'icon-maskable-512.png', bg: false },
  { svg: bleed, size: 180, file: 'apple-touch-icon.png', bg: false },
]

const browser = await chromium.launch()
try {
  for (const { svg, size, file, bg } of targets) {
    const page = await browser.newPage({
      viewport: { width: size, height: size },
      deviceScaleFactor: 1,
    })
    await page.setContent(
      `<!doctype html><html><body style="margin:0;padding:0">` +
        `<img width="${size}" height="${size}" src="data:image/svg+xml;utf8,${encodeURIComponent(svg)}" />` +
        `</body></html>`,
    )
    await page.locator('img').waitFor()
    const buffer = await page.screenshot({
      omitBackground: bg, // rounded tile keeps transparent corners; full-bleed does not
      clip: { x: 0, y: 0, width: size, height: size },
    })
    fs.writeFileSync(path.join(publicDir, file), buffer)
    // eslint-disable-next-line no-console
    console.log(`wrote public/${file} (${size}x${size})`)
    await page.close()
  }
} finally {
  await browser.close()
}
