/**
 * Statistik — per-person "who paid how much, when" for the active household
 * (docs/specs/household-statistics.md). v1 is a single line chart of each
 * member's monthly contributions over the trailing 12 months. Aggregation is
 * server-side (ADR-0006); this screen only renders and calls. No design export
 * exists for this new screen — layout mirrors the other list screens (header +
 * card), using semantic tokens and each member's avatar colour as their series.
 */
import { createFileRoute } from '@tanstack/react-router'
import { CartesianGrid, Line, LineChart, XAxis, YAxis } from 'recharts'
import { RequireAuth } from '@/components/require-auth'
import { Card } from '@/components/ui/card'
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from '@/components/ui/chart'
import { EmptyState, ErrorState, LoadingState, NoHouseholdState } from '@/components/screen-states'
import { useActiveHousehold } from '@/lib/active-household'
import { useContributionStats } from '@/lib/queries'
import { useSheet } from '@/lib/sheet'
import { formatKr, monthLabel } from '@/lib/format'
import type { ContributionStatsDto } from '@/lib/api'

export const Route = createFileRoute('/statistik')({
  component: () => (
    <RequireAuth>
      <StatistikPage />
    </RequireAuth>
  ),
})

const EMPTY_COPY =
  'Inget att visa än. När någon lägger till utgifter dyker det upp här — vem som la ut vad, månad för månad.'
const FOOTER_COPY =
  'Visar vem som lagt ut hur mycket per månad, de senaste 12 månaderna. Bara vem som betalat — inte vem som är skyldig vad.'

function StatistikPage() {
  const { householdId, households, isLoading: householdsLoading } = useActiveHousehold()
  const { openSheet } = useSheet()
  const statsQuery = useContributionStats(householdId)

  if (!householdsLoading && households.length === 0) {
    return <NoHouseholdState onCreate={() => openSheet('newHousehold')} className="mt-6" />
  }

  return (
    <div className="flex flex-col gap-4">
      <header>
        <h1 className="font-heading text-[19px] font-bold tracking-tight">Statistik</h1>
        <p className="mt-0.5 text-[12.5px] text-muted-foreground">
          Vem la ut vad, månad för månad.
        </p>
      </header>

      <StatistikBody householdId={householdId} query={statsQuery} />

      <p className="px-5 text-center text-xs leading-relaxed text-muted-foreground text-balance">
        {FOOTER_COPY}
      </p>
    </div>
  )
}

function StatistikBody({
  householdId,
  query,
}: {
  householdId: string | undefined
  query: ReturnType<typeof useContributionStats>
}) {
  // Guard while the active household is still resolving.
  if (!householdId || query.isPending) {
    return <LoadingState rows={1} hero />
  }

  if (query.isError) {
    return <ErrorState error={query.error} onRetry={() => query.refetch()} />
  }

  const stats = query.data
  if (!stats || stats.members.length === 0) {
    return <EmptyState className="py-12">{EMPTY_COPY}</EmptyState>
  }

  return (
    <Card className="p-4">
      <ContributionChart stats={stats} />
    </Card>
  )
}

function ContributionChart({ stats }: { stats: ContributionStatsDto }) {
  // One series per member; each keyed by memberId so the chart's --color-<key>
  // vars carry the member's avatar colour. Values stay in minor units and are
  // formatted with formatKr for the axis + tooltip.
  const chartConfig: ChartConfig = Object.fromEntries(
    stats.members.map((m) => [m.memberId, { label: m.name, color: m.avatarColor }]),
  )

  const chartData = stats.buckets.map((bucket) => {
    const row: Record<string, string | number> = { month: monthLabel(bucket.month) }
    for (const pm of bucket.perMember) row[pm.memberId] = Number(pm.paidMinor)
    return row
  })

  return (
    <ChartContainer config={chartConfig} className="h-[280px] w-full">
      <LineChart accessibilityLayer data={chartData} margin={{ left: 12, right: 12 }}>
        <CartesianGrid vertical={false} />
        <XAxis
          dataKey="month"
          tickLine={false}
          axisLine={false}
          tickMargin={8}
          minTickGap={4}
        />
        <YAxis
          tickLine={false}
          axisLine={false}
          width={56}
          tickFormatter={(value) => formatKr(value as number)}
        />
        <ChartTooltip
          content={<ChartTooltipContent formatter={(value) => formatKr(value as number)} />}
        />
        <ChartLegend content={<ChartLegendContent />} />
        {stats.members.map((m) => (
          <Line
            key={m.memberId}
            dataKey={m.memberId}
            type="monotone"
            stroke={`var(--color-${m.memberId})`}
            strokeWidth={2}
            dot={false}
          />
        ))}
      </LineChart>
    </ChartContainer>
  )
}
