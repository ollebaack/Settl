/**
 * Multi-household overview (ADR-0019 + docs/design/multi-household-overview-addendum.md,
 * frames 1–2). Rendered at `/` only when the user has ≥2 ACTIVE households — the
 * single-household majority never sees it. Lists every active book as a card
 * with its server-derived net (HouseholdListItemDto), a roll-up hero, the
 * "add a friend" affordance, and an archived section.
 *
 * Currency rule (load-bearing): the hero sums nets ONLY when every active book
 * shares one currency. With ≥2 distinct currencies it shows a descriptive
 * roll-up instead — never a cross-currency sum. The screen renders and calls;
 * the per-book net/label are server-derived (ADR-0006). The only client maths is
 * the same-currency roll-up, which the addendum (§5.2) explicitly assigns to the
 * client.
 */
import { useNavigate } from '@tanstack/react-router'
import { useQueries } from '@tanstack/react-query'
import { PlusIcon } from 'lucide-react'
import { Card } from '@/components/ui/card'
import { HouseholdBadge } from '@/components/household-badge'
import { useActiveHousehold } from '@/lib/active-household'
import { useSheet } from '@/lib/sheet'
import { apiGet, type HouseholdListItemDto, type HouseholdSummaryDto } from '@/lib/api'
import { queryKeys } from '@/lib/queries'
import { formatSignedMoney } from '@/lib/format'
import { cn } from '@/lib/utils'

const SECTION_LABEL =
  'text-xs font-semibold uppercase tracking-[0.09em] text-muted-foreground'

/** Coerce an int64-as-`number | string` (openapi) field to a number. */
function toNum(value: number | string): number {
  return typeof value === 'number' ? value : Number(value)
}

/** Net-label → per-book sub copy + amount colour (server-derived label, ADR-0006). */
function netSub(label: string): string {
  if (label === 'owed') return 'du ska få'
  if (label === 'owe') return 'du är skyldig'
  return 'kvitt'
}
function netTextClass(label: string): string {
  if (label === 'owed') return 'text-success'
  if (label === 'owe') return 'text-destructive'
  return 'text-muted-foreground'
}

export function Overview({ households }: { households: HouseholdListItemDto[] }) {
  const navigate = useNavigate()
  const { setHouseholdId } = useActiveHousehold()
  const { openSheet } = useSheet()

  // Client detects >1 distinct currency across active books (ADR-0019 §2.2).
  const distinctCurrencies = new Set(households.map((h) => h.currency))
  const singleCurrency = distinctCurrencies.size === 1

  // Tapping a book sets the active household and enters its dashboard.
  const enter = (h: HouseholdListItemDto) => {
    setHouseholdId(h.id)
    navigate({ to: '/hushall/$id', params: { id: h.id }, search: {} })
  }

  return (
    <div className="flex flex-col gap-5">
      <header>
        <p className={SECTION_LABEL}>Översikt</p>
        <h1 className="mt-0.5 font-heading text-2xl font-semibold tracking-tight">
          Dina hushåll
        </h1>
      </header>

      {singleCurrency ? (
        <SingleCurrencyHero households={households} currency={households[0].currency} />
      ) : (
        <MixedCurrencyHero households={households} />
      )}

      <section>
        <p className={SECTION_LABEL}>Hushåll</p>
        <div className="mt-2 flex flex-col gap-2.5">
          {households.map((h) => (
            <HouseholdCard
              key={h.id}
              household={h}
              showCurrency={!singleCurrency}
              onEnter={() => enter(h)}
            />
          ))}
          <button
            type="button"
            onClick={() => openSheet('newHousehold')}
            className="w-full rounded-2xl border-[1.5px] border-dashed border-border py-3.5 text-center text-[13px] font-semibold text-muted-foreground outline-none transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring"
          >
            + Nytt hushåll
          </button>
        </div>
        {!singleCurrency && (
          <p className="mt-3 text-[12.5px] text-muted-foreground">
            Saldot per hushåll är i hushållets egen valuta. Inget kombinerat totalbelopp
            visas när valutorna skiljer sig.
          </p>
        )}
      </section>

      <FriendsAffordance onOpen={() => openSheet('addFriend')} />

      <ArchivedSection households={households} />
    </div>
  )
}

// --- Roll-up heroes ---------------------------------------------------------

/**
 * Same-currency hero: one summed net headline. The open-item count for the
 * sub-line isn't on HouseholdListItemDto, so we piggyback on the per-household
 * summaries (addendum §2.1 / §5.2) — cached + reused when a book is entered.
 */
function SingleCurrencyHero({
  households,
  currency,
}: {
  households: HouseholdListItemDto[]
  currency: string
}) {
  const summaries = useQueries({
    queries: households.map((h) => ({
      queryKey: queryKeys.summary(h.id),
      queryFn: () => apiGet<HouseholdSummaryDto>(`/households/${h.id}/summary`),
    })),
  })
  const allLoaded = summaries.every((s) => s.data)
  const openCount = summaries.reduce((sum, s) => sum + (s.data ? toNum(s.data.openCount) : 0), 0)

  const total = households.reduce((sum, h) => sum + toNum(h.netMinor), 0)
  const label =
    total > 0 ? 'Du ska få totalt' : total < 0 ? 'Du är skyldig totalt' : 'Allt är kvitt'
  const intentClass =
    total > 0 ? 'text-success' : total < 0 ? 'text-destructive' : 'text-muted-foreground'

  const sub = allLoaded
    ? `över ${households.length} aktiva hushåll · ${openCount} öppna poster`
    : `över ${households.length} aktiva hushåll`

  return (
    <Card className="items-center gap-1.5 px-5 py-6 text-center">
      <p className={SECTION_LABEL}>{label}</p>
      <span
        className={cn('font-mono tabular-nums text-[34px] leading-none tracking-tight', intentClass)}
      >
        {formatSignedMoney(total, currency)}
      </span>
      <p className="text-[12.5px] text-muted-foreground">{sub}</p>
    </Card>
  )
}

/** Mixed-currency hero: descriptive roll-up, never a sum (ADR-0019 §2.2). */
function MixedCurrencyHero({ households }: { households: HouseholdListItemDto[] }) {
  const owedCount = households.filter((h) => h.netLabel === 'owed').length
  const oweCount = households.filter((h) => h.netLabel === 'owe').length

  return (
    <Card className="gap-0 px-5 py-5">
      <p className={SECTION_LABEL}>Din ställning</p>
      <div className="mt-3 flex flex-col gap-2.5">
        <div className="flex items-center justify-between">
          <span className="text-[13.5px]">Du ska få i</span>
          <span className="text-sm font-bold text-success">{owedCount} hushåll</span>
        </div>
        <div className="flex items-center justify-between">
          <span className="text-[13.5px]">Du är skyldig i</span>
          <span className="text-sm font-bold text-destructive">{oweCount} hushåll</span>
        </div>
      </div>
      <p className="mt-3 text-[12.5px] text-muted-foreground">
        Olika valutor — summeras inte. Öppna ett hushåll för dess saldo.
      </p>
    </Card>
  )
}

// --- Household card ----------------------------------------------------------

function HouseholdCard({
  household,
  showCurrency,
  onEnter,
}: {
  household: HouseholdListItemDto
  showCurrency: boolean
  onEnter: () => void
}) {
  const memberLine = showCurrency
    ? `${household.memberNames.join(', ')} · ${household.currency}`
    : household.memberNames.join(', ')

  return (
    <Card
      role="button"
      tabIndex={0}
      data-testid="household-card"
      onClick={onEnter}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onEnter()
        }
      }}
      size="sm"
      className="flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring"
    >
      <HouseholdBadge name={household.name} size="lg" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-bold">{household.name}</p>
        <p className="truncate text-[11.5px] text-muted-foreground">{memberLine}</p>
      </div>
      <div className="flex shrink-0 flex-col items-end gap-0.5">
        <span
          className={cn(
            'font-mono tabular-nums text-sm font-semibold',
            netTextClass(household.netLabel),
          )}
        >
          {formatSignedMoney(household.netMinor, household.currency)}
        </span>
        <span className="text-[10.5px] text-muted-foreground">{netSub(household.netLabel)}</span>
      </div>
    </Card>
  )
}

// --- "Add a friend" affordance (ADR-0019 §2.4) ------------------------------

/**
 * Loose affordance + copy only. The contacts model (add-by-number = blind SMS
 * invite, contacts reusable across households) is a SEPARATE workstream
 * (docs/adr/0019-contacts-by-phone-and-sms-invites.md) — this carries just the
 * entry point and copy, no contact data or endpoints.
 */
function FriendsAffordance({ onOpen }: { onOpen: () => void }) {
  return (
    <section>
      <p className={SECTION_LABEL}>Vänner</p>
      <Card
        role="button"
        tabIndex={0}
        data-testid="add-friend-card"
        onClick={onOpen}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault()
            onOpen()
          }
        }}
        size="sm"
        className="mt-2 flex flex-row items-center gap-3 p-3 text-left transition-colors hover:bg-muted/40 focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span aria-hidden="true" className="flex items-center">
          {['bg-[#f0dcc3]', 'bg-[#d9e0ee]', 'bg-[#dfe6cf]'].map((bg, i) => (
            <span
              key={i}
              className={cn(
                'size-[30px] rounded-full ring-2 ring-card',
                bg,
                i > 0 && '-ml-3.5',
              )}
            />
          ))}
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-[13.5px] font-bold">Lägg till en vän</p>
          <p className="text-[11.5px] text-muted-foreground">Bjud in via nummer eller mejl</p>
        </div>
        <span
          aria-hidden="true"
          className="grid size-[34px] place-items-center rounded-full border-[1.5px] border-dashed border-border text-muted-foreground"
        >
          <PlusIcon className="size-4" />
        </span>
      </Card>
    </section>
  )
}

// --- Archived section (ADR-0016) --------------------------------------------

/**
 * Archived households render as dimmed rows with an owner-only `Återställ` chip
 * (design §2.5). ADR-0016 (household archival) is NOT built on this branch:
 * GET /households returns active books only and HouseholdListItemDto carries no
 * `archivedAt`/`isOwner`, so there is nothing to list yet and the section stays
 * hidden. When archival lands (adds an archived scope + those fields, plus
 * POST /households/{id}/restore), feed the archived list in here — the row +
 * owner-gated restore below are ready. See addendum §2.5 / §5.6.
 */
interface ArchivedHousehold {
  id: string
  name: string
  currency: string
  /** Localised archived date, e.g. "4 jul". */
  archivedLabel: string
  isOwner: boolean
}

function ArchivedSection({ households: _households }: { households: HouseholdListItemDto[] }) {
  const archived: ArchivedHousehold[] = []
  if (archived.length === 0) return null

  return (
    <section>
      <p className={SECTION_LABEL}>Arkiverade</p>
      <div className="mt-2 flex flex-col gap-2">
        {archived.map((h) => (
          <Card
            key={h.id}
            size="sm"
            className="flex flex-row items-center gap-3 p-3 opacity-60"
          >
            <HouseholdBadge name={h.name} size="md" className="grayscale" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold">{h.name}</p>
              <p className="truncate text-[11px] text-muted-foreground">
                Arkiverad {h.archivedLabel} · {h.currency}
              </p>
            </div>
            {h.isOwner && (
              <button
                type="button"
                className="rounded-full bg-muted px-3 py-1.5 text-xs font-semibold text-primary outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                Återställ
              </button>
            )}
          </Card>
        ))}
      </div>
      <p className="mt-2 text-[12.5px] text-muted-foreground">
        Bara ägaren ser återställ-knappen.
      </p>
    </section>
  )
}
