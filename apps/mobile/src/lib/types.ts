// The API is authoritative over shapes (ADR-0006); these are the generated OpenAPI types the
// thin-slice screens render. Regenerate `@settl/api-client` whenever the API shape changes.
import type { components } from '@settl/api-client';

type Schemas = components['schemas'];

export type HouseholdListItem = Schemas['HouseholdListItemDto'];
export type HouseholdSummary = Schemas['HouseholdSummaryDto'];
export type PersonBalance = Schemas['PersonBalanceDto'];
export type Entry = Schemas['EntryDto'];
export type AccessTokenResponse = Schemas['AccessTokenResponse'];
