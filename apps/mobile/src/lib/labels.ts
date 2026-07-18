// The API returns semantic balance CODES, not display text (ADR-0006) — the client owns copy
// and colour. Kept in lockstep with apps/web (overview.tsx / household-book.tsx) so both
// surfaces read identically.

/** Household-hero copy for `HouseholdSummaryDto.netLabel` (owed | owe | square). */
export function netHeroLabel(netLabel: string): string {
  if (netLabel === 'owed') return 'Du ska få totalt';
  if (netLabel === 'owe') return 'Du är skyldig totalt';
  return 'Allt är kvitt';
}

/** Compact list-card copy for `HouseholdListItemDto.netLabel`. */
export function netSubLabel(netLabel: string): string {
  if (netLabel === 'owed') return 'du ska få';
  if (netLabel === 'owe') return 'du är skyldig';
  return 'kvitt';
}

/** Per-person copy for `PersonBalanceDto.relation` (owesYou | youOwe | square). */
export function relationLabel(relation: string): string {
  if (relation === 'owesYou') return 'är skyldig dig';
  if (relation === 'youOwe') return 'du är skyldig';
  return 'kvitt';
}

/** Tailwind text-colour for a balance code (positive = green, negative = red, even = muted). */
export function balanceColorClass(code: string): string {
  if (code === 'owed' || code === 'owesYou') return 'text-green-600';
  if (code === 'owe' || code === 'youOwe') return 'text-red-600';
  return 'text-neutral-500';
}
