/**
 * Ny post — create an expense, an IOU (Lån), or a recurring template.
 * Implementation-map §2.6 + flow §4. Three-mode split editor with LIVE,
 * color-coded validation; the API is authoritative (ADR-0006) so client
 * pre-checks raise the exact toast copy and API error details are surfaced too.
 */
import { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { Loader2Icon } from 'lucide-react'
import { toast } from 'sonner'

import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { LoadingState, ErrorState } from '@/components/screen-states'
import { MemberAvatar } from '@/components/member-avatar'
import { Money } from '@/components/money'

import { useActiveHousehold } from '@/lib/active-household'
import { useDevUser } from '@/lib/dev-user'
import { useMembers, useCreateEntry, useCreateRecurring } from '@/lib/queries'
import { formatKr, shortDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import type {
  CreateEntryRequest,
  CreateRecurringRequest,
  MemberDto,
  SplitInput,
} from '@/lib/api'

type EntryTab = 'expense' | 'iou' | 'recurring'
type SplitMode = 'equal' | 'percent' | 'amount'
type IouDir = 'iowe' | 'theyowe'
type Cadence = 'monthly' | 'biweekly' | 'weekly'

const TITLE_PLACEHOLDER: Record<EntryTab, string> = {
  expense: 'Mat, middag, biljetter…',
  iou: 'Vad gäller det?',
  recurring: 'Hyra, internet, Spotify…',
}

const SAVE_LABEL: Record<EntryTab, string> = {
  expense: 'Lägg i loggboken',
  iou: 'Anteckna',
  recurring: 'Sätt på repeat',
}

/** Uppercase tracked section field label (dc: 11.5px / .09em / uppercase). */
const FIELD_LABEL =
  'text-[11.5px] font-semibold uppercase tracking-[0.09em] text-muted-foreground'

/** `1 234,50` → 123450 (öre). Accepts sv-SE comma decimals + space grouping. */
function parseAmountMinor(raw: string): number {
  const n = parseFloat(raw.replace(/\s/g, '').replace(',', '.'))
  return Number.isFinite(n) ? Math.round(n * 100) : 0
}

/** Plain number parse (percent inputs): comma-decimal aware. */
function parseNum(raw: string): number {
  const n = parseFloat(raw.replace(/\s/g, '').replace(',', '.'))
  return Number.isFinite(n) ? n : 0
}

function formatNum(n: number): string {
  return n.toLocaleString('sv-SE', { maximumFractionDigits: 2 })
}

function todayIso(): string {
  const d = new Date()
  const p = (v: number) => String(v).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

/** Base UI toggle groups are single-select but hand back an array — keep a value. */
function pickSingle<T extends string>(next: string[], current: T): T {
  return (next[0] as T | undefined) ?? current
}

export function AddEntrySheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { householdId } = useActiveHousehold()
  const { memberId: currentMemberId } = useDevUser()
  const { data: members, isLoading, isError, error, refetch } = useMembers(householdId)
  const createEntry = useCreateEntry(householdId)
  const createRecurring = useCreateRecurring(householdId)
  const navigate = useNavigate()

  const [type, setType] = useState<EntryTab>('expense')
  const [amountStr, setAmountStr] = useState('')
  const [title, setTitle] = useState('')
  const [paidBy, setPaidBy] = useState<string | null>(null)
  const [splitMode, setSplitMode] = useState<SplitMode>('equal')
  const [vals, setVals] = useState<Record<string, string>>({})
  const [iouDir, setIouDir] = useState<IouDir>('iowe')
  const [iouWith, setIouWith] = useState<string | null>(null)
  const [cadence, setCadence] = useState<Cadence>('monthly')
  const [date, setDate] = useState(todayIso())

  const submitting = createEntry.isPending || createRecurring.isPending
  const totalMinor = parseAmountMinor(amountStr)

  function resetForm() {
    setType('expense')
    setAmountStr('')
    setTitle('')
    setPaidBy(null)
    setSplitMode('equal')
    setVals({})
    setIouDir('iowe')
    setIouWith(null)
    setCadence('monthly')
    setDate(todayIso())
  }

  function changeSplitMode(next: string[]) {
    const mode = pickSingle<SplitMode>(next, splitMode)
    setSplitMode(mode)
    setVals({}) // percent numbers must not be reused as kr amounts
  }

  const memberList: MemberDto[] = members ?? []
  const otherMembers = memberList.filter((m) => m.id !== currentMemberId)
  const payer = paidBy ?? currentMemberId ?? memberList[0]?.id
  const iouOther = iouWith ?? otherMembers[0]?.id

  // --- live split derivations ------------------------------------------------
  const equalShareMinor =
    memberList.length > 0 ? Math.round(totalMinor / memberList.length) : 0
  const percentSum = memberList.reduce((sum, m) => sum + parseNum(vals[m.id] ?? ''), 0)
  const amountSumMinor = memberList.reduce(
    (sum, m) => sum + parseAmountMinor(vals[m.id] ?? ''),
    0,
  )
  const percentOk = Math.abs(percentSum - 100) <= 0.5
  const amountOk = Math.abs(amountSumMinor - totalMinor) <= 5 // ±0.05 kr

  function buildSplit(): SplitInput {
    if (splitMode === 'equal') return { mode: 'equal', values: null }
    const values: Record<string, number> = {}
    for (const m of memberList) {
      values[m.id] =
        splitMode === 'percent'
          ? parseNum(vals[m.id] ?? '')
          : parseAmountMinor(vals[m.id] ?? '')
    }
    return { mode: splitMode, values }
  }

  async function onSave() {
    if (totalMinor <= 0) {
      toast('Ange ett belopp först')
      return
    }
    if (type !== 'iou') {
      if (splitMode === 'percent' && !percentOk) {
        toast('Procenten måste bli 100')
        return
      }
      if (splitMode === 'amount' && !amountOk) {
        toast(`Delningen måste bli ${formatKr(totalMinor)}`)
        return
      }
    }

    try {
      if (type === 'recurring') {
        const body: CreateRecurringRequest = {
          title: title.trim() || 'Utan titel',
          amountMinor: totalMinor,
          cadence,
          nextPostDate: date,
          paidByMemberId: payer as string,
          split: buildSplit(),
        }
        await createRecurring.mutateAsync(body)
        toast(`På repeat — bokförs först ${shortDate(date)}`)
        resetForm()
        navigate({ to: '/recurring' })
        return
      }

      const body: CreateEntryRequest =
        type === 'iou'
          ? {
              type: 'iou',
              title: title.trim() || 'Lån',
              amountMinor: totalMinor,
              date: null,
              paidByMemberId: null,
              fromMemberId:
                iouDir === 'iowe' ? (currentMemberId ?? null) : (iouOther ?? null),
              toMemberId:
                iouDir === 'iowe' ? (iouOther ?? null) : (currentMemberId ?? null),
              split: null,
            }
          : {
              type: 'expense',
              title: title.trim() || 'Utan titel',
              amountMinor: totalMinor,
              date: null,
              paidByMemberId: payer ?? null,
              fromMemberId: null,
              toMemberId: null,
              split: buildSplit(),
            }
      await createEntry.mutateAsync(body)
      toast(type === 'iou' ? 'Antecknat' : 'Tillagd i loggboken')
      resetForm()
      onClose()
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Ny post"
    >
      {isLoading ? (
        <LoadingState rows={4} />
      ) : isError ? (
        <ErrorState error={error} onRetry={() => refetch()} />
      ) : (
        <div className="flex flex-col gap-5">
          {/* Type picker */}
          <Tabs
            value={type}
            onValueChange={(v) => setType(v as EntryTab)}
            className="w-full"
          >
            <TabsList className="w-full">
              <TabsTrigger value="expense">Utgift</TabsTrigger>
              <TabsTrigger value="iou">Lån</TabsTrigger>
              <TabsTrigger value="recurring">Återkommande</TabsTrigger>
            </TabsList>
          </Tabs>

          {/* Amount */}
          <div className="relative flex items-baseline">
            <Input
              inputMode="decimal"
              placeholder="0,00"
              value={amountStr}
              onChange={(e) => setAmountStr(e.target.value)}
              aria-label="Belopp"
              className="h-auto border-transparent bg-transparent px-0 pr-9 text-right font-mono text-[38px] leading-tight shadow-none focus-visible:ring-0"
            />
            <span className="pointer-events-none absolute right-0 bottom-1 font-mono text-lg text-muted-foreground">
              kr
            </span>
          </div>

          {/* Title — visible label dropped; aria-label + placeholder only */}
          <Input
            aria-label="Titel"
            placeholder={TITLE_PLACEHOLDER[type]}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />

          {/* IOU branch */}
          {type === 'iou' ? (
            <>
              <div className="flex flex-col gap-2">
                <Label className={FIELD_LABEL}>Åt vilket håll</Label>
                <ToggleGroup
                  value={[iouDir]}
                  onValueChange={(v) => setIouDir(pickSingle<IouDir>(v, iouDir))}
                  variant="outline"
                  className="w-full"
                >
                  <ToggleGroupItem value="iowe" className="flex-1">
                    Jag är skyldig
                  </ToggleGroupItem>
                  <ToggleGroupItem value="theyowe" className="flex-1">
                    Skyldig mig
                  </ToggleGroupItem>
                </ToggleGroup>
              </div>

              <div className="flex flex-col gap-2">
                <Label className={FIELD_LABEL}>Med</Label>
                <ToggleGroup
                  value={iouOther ? [iouOther] : []}
                  onValueChange={(v) => setIouWith(pickSingle(v, iouOther ?? ''))}
                  variant="outline"
                  className="flex-wrap"
                >
                  {otherMembers.map((m) => (
                    <ToggleGroupItem key={m.id} value={m.id} className="gap-2">
                      <MemberAvatar name={m.name} avatarColor={m.avatarColor} size="sm" />
                      {m.name}
                    </ToggleGroupItem>
                  ))}
                </ToggleGroup>
              </div>
            </>
          ) : (
            <>
              {/* Payer */}
              <div className="flex flex-col gap-2">
                <Label className={FIELD_LABEL}>Vem betalade</Label>
                <ToggleGroup
                  value={payer ? [payer] : []}
                  onValueChange={(v) => setPaidBy(pickSingle(v, payer ?? ''))}
                  variant="outline"
                  className="flex-wrap"
                >
                  {memberList.map((m) => (
                    <ToggleGroupItem key={m.id} value={m.id} className="gap-2">
                      <MemberAvatar name={m.name} avatarColor={m.avatarColor} size="sm" />
                      {m.name}
                    </ToggleGroupItem>
                  ))}
                </ToggleGroup>
              </div>

              {/* Split mode */}
              <div className="flex flex-col gap-2">
                <Label className={FIELD_LABEL}>Delning</Label>
                <ToggleGroup
                  value={[splitMode]}
                  onValueChange={changeSplitMode}
                  variant="outline"
                >
                  <ToggleGroupItem value="equal" className="flex-1">
                    Lika
                  </ToggleGroupItem>
                  <ToggleGroupItem value="percent" className="flex-1">
                    %
                  </ToggleGroupItem>
                  <ToggleGroupItem value="amount" className="flex-1">
                    kr
                  </ToggleGroupItem>
                </ToggleGroup>

                <Card size="sm" className="gap-0 py-2">
                  {memberList.map((m, i) => (
                    <div
                      key={m.id}
                      className={cn(
                        'flex items-center justify-between gap-3 px-4 py-2',
                        i > 0 && 'border-t border-border',
                      )}
                    >
                      <div className="flex items-center gap-2">
                        <MemberAvatar
                          name={m.name}
                          avatarColor={m.avatarColor}
                          size="sm"
                        />
                        <span className="text-sm">{m.name}</span>
                      </div>

                      {splitMode === 'equal' ? (
                        totalMinor > 0 ? (
                          <Money minor={equalShareMinor} intent="muted" className="text-sm" />
                        ) : (
                          <span className="font-mono text-sm text-muted-foreground">—</span>
                        )
                      ) : (
                        <div className="relative w-28">
                          <Input
                            inputMode="decimal"
                            value={vals[m.id] ?? ''}
                            onChange={(e) =>
                              setVals((prev) => ({ ...prev, [m.id]: e.target.value }))
                            }
                            aria-label={`${m.name} andel`}
                            className="pr-8 text-right font-mono"
                          />
                          <span className="pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 text-xs text-muted-foreground">
                            {splitMode === 'percent' ? '%' : 'kr'}
                          </span>
                        </div>
                      )}
                    </div>
                  ))}

                  {/* Live split hint — right-aligned final row inside the Card */}
                  <div className="flex justify-end border-t border-border px-4 py-2.5 text-right">
                    <SplitHint
                      splitMode={splitMode}
                      totalMinor={totalMinor}
                      equalShareMinor={equalShareMinor}
                      percentSum={percentSum}
                      percentOk={percentOk}
                      amountSumMinor={amountSumMinor}
                      amountOk={amountOk}
                    />
                  </div>
                </Card>
              </div>

              {/* Recurring extras */}
              {type === 'recurring' && (
                <>
                  <div className="flex flex-col gap-2">
                    <Label className={FIELD_LABEL}>Upprepas</Label>
                    <ToggleGroup
                      value={[cadence]}
                      onValueChange={(v) => setCadence(pickSingle<Cadence>(v, cadence))}
                      variant="outline"
                    >
                      <ToggleGroupItem value="monthly" className="flex-1">
                        Varje månad
                      </ToggleGroupItem>
                      <ToggleGroupItem value="biweekly" className="flex-1">
                        Varannan vecka
                      </ToggleGroupItem>
                      <ToggleGroupItem value="weekly" className="flex-1">
                        Varje vecka
                      </ToggleGroupItem>
                    </ToggleGroup>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="recurring-date">Bokförs först</Label>
                    <Input
                      id="recurring-date"
                      type="date"
                      value={date}
                      onChange={(e) => setDate(e.target.value)}
                    />
                  </div>

                  <p className="text-xs text-muted-foreground">
                    Settl bokför den i loggboken varje period och delar om
                    automatiskt. Ställ in en gång, glöm den sen.
                  </p>
                </>
              )}
            </>
          )}

          {/* Save */}
          <Button
            type="button"
            onClick={onSave}
            disabled={submitting}
            className={cn('mt-1 w-full', totalMinor <= 0 && 'opacity-45')}
          >
            {submitting && <Loader2Icon className="animate-spin" />}
            {SAVE_LABEL[type]}
          </Button>
        </div>
      )}
    </ResponsiveSheet>
  )
}

function SplitHint({
  splitMode,
  totalMinor,
  equalShareMinor,
  percentSum,
  percentOk,
  amountSumMinor,
  amountOk,
}: {
  splitMode: SplitMode
  totalMinor: number
  equalShareMinor: number
  percentSum: number
  percentOk: boolean
  amountSumMinor: number
  amountOk: boolean
}) {
  if (splitMode === 'equal') {
    return (
      <p className="text-xs font-semibold text-muted-foreground">
        {totalMinor > 0 ? (
          <>
            <Money minor={equalShareMinor} intent="muted" className="text-xs font-semibold" /> var
          </>
        ) : (
          'Alla betalar lika mycket'
        )}
      </p>
    )
  }

  if (splitMode === 'percent') {
    return (
      <p className={cn('text-xs font-semibold', percentOk ? 'text-success' : 'text-destructive')}>
        {formatNum(percentSum)} % av 100 % fördelat
      </p>
    )
  }

  return (
    <p className={cn('text-xs font-semibold', amountOk ? 'text-success' : 'text-destructive')}>
      {formatKr(amountSumMinor)} av {formatKr(totalMinor)} fördelat
    </p>
  )
}
