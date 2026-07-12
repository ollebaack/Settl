/**
 * Formatting helpers. Money = integer minor units (öre); dates are UTC ISO or
 * calendar (yyyy-MM-dd). See implementation-map §7. Never hardwire "today".
 */

const SV_MONTHS = [
  'jan', 'feb', 'mar', 'apr', 'maj', 'jun',
  'jul', 'aug', 'sep', 'okt', 'nov', 'dec',
] as const

const NBSP = ' '
const MINUS = '−' // U+2212 true minus

/** Coerce an int64-as-`number | string` (openapi) money field to a number. */
function toNumber(minor: number | string): number {
  return typeof minor === 'number' ? minor : Number(minor)
}

/** `1 234,50 kr` — absolute value, sv-SE grouping, non-breaking space before kr. */
export function formatKr(minor: number | string): string {
  const major = Math.abs(toNumber(minor) / 100)
  const num = major.toLocaleString('sv-SE', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  })
  return `${num}${NBSP}kr`
}

/**
 * Signed money for the settle sheet: `+1 234 kr` / `−1 234 kr` (true minus).
 * Zero renders without a sign.
 */
export function formatSignedKr(minor: number | string): string {
  const n = toNumber(minor)
  const base = formatKr(n)
  if (n > 0) return `+${base}`
  if (n < 0) return `${MINUS}${base}`
  return base
}

/** Parse a calendar date (yyyy-MM-dd) as local midnight; fall back to Date(iso). */
function parseLocal(iso: string): Date {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso)
  if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]))
  return new Date(iso)
}

function startOfToday(): Date {
  const now = new Date()
  return new Date(now.getFullYear(), now.getMonth(), now.getDate())
}

/** Whole calendar days from today to `iso` (negative = in the past). */
export function daysFromToday(iso: string): number {
  const target = parseLocal(iso)
  const targetMidnight = new Date(
    target.getFullYear(),
    target.getMonth(),
    target.getDate(),
  )
  const MS_PER_DAY = 86_400_000
  return Math.round((targetMidnight.getTime() - startOfToday().getTime()) / MS_PER_DAY)
}

/** `11 jul` — day + Swedish month abbreviation, no leading zero. */
export function shortDate(iso: string): string {
  const d = parseLocal(iso)
  return `${d.getDate()} ${SV_MONTHS[d.getMonth()]}`
}

/** Relative day phrase: `idag` / `imorgon` / `om N dagar` (and past variants). */
export function inDays(iso: string): string {
  const n = daysFromToday(iso)
  if (n === 0) return 'idag'
  if (n === 1) return 'imorgon'
  if (n === -1) return 'igår'
  if (n > 1) return `om ${n} dagar`
  return `för ${Math.abs(n)} dagar sedan`
}

/** Ledger day-group header: `Idag` / `Igår` / `{d} {mån}`. */
export function dayGroupLabel(iso: string): string {
  const n = daysFromToday(iso)
  if (n === 0) return 'Idag'
  if (n === -1) return 'Igår'
  return shortDate(iso)
}
