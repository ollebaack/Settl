// Money is integer minor units (öre); the API owns all math (ADR-0006), the client only formats.
// Hand-rolled sv-SE formatting (space thousands separator, comma decimal) rather than Intl —
// Hermes' Intl currency support is uneven, and this stays deterministic.
// int64 fields arrive as `number | string` from the generated client (openapi-typescript
// widens int64 to avoid precision loss), so coerce at this boundary.
export function formatMoney(minor: number | string, currency = 'SEK'): string {
  const value = typeof minor === 'string' ? Number(minor) : minor;
  const negative = value < 0;
  const abs = Math.abs(value);
  const kronor = Math.floor(abs / 100);
  const ore = abs % 100;
  const grouped = String(kronor).replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
  const amount = ore === 0 ? grouped : `${grouped},${String(ore).padStart(2, '0')}`;
  return `${negative ? '-' : ''}${amount} ${currency}`;
}

const SV_MONTHS = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];

// `iso` is a DateOnly ("yyyy-MM-dd"). Renders a short "d mmm" (e.g. "18 jul").
export function formatDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number);
  if (!year || !month || !day) return iso;
  return `${day} ${SV_MONTHS[month - 1]}`;
}
