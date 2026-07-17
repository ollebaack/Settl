/**
 * Ny post / Redigera post — create or edit an expense or a recurring template.
 * Implementation-map §2.6 + flow §4 + ledger-editing addendum §2.1–2.2. Split editor
 * (Lika / Allt på en / % / kr) where "Allt på en" puts the whole amount on one person
 * (ADR-0020 removed the separate IOU type), with LIVE, color-coded validation; the API is authoritative
 * (ADR-0006) so client pre-checks raise the exact toast copy and API error details
 * are surfaced too. Edit mode reuses the same form, prefilled from the entry /
 * template (PUT /entries/{id} · PATCH /recurring/{id}).
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
import {
  useMe,
  useMembers,
  useCreateEntry,
  useCreateRecurring,
  useUpdateEntry,
  useUpdateRecurring,
  useEntry,
  useRecurringDetail,
} from '@/lib/queries'
import { formatKr, shortDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import type {
  CreateEntryRequest,
  CreateRecurringRequest,
  EntryDto,
  MemberDto,
  RecurringDetailDto,
  SplitInput,
  UpdateEntryRequest,
  UpdateRecurringRequest,
} from '@/lib/api'

type EntryTab = 'expense' | 'recurring'
type SplitMode = 'equal' | 'percent' | 'amount' | 'whole'
type Cadence = 'monthly' | 'biweekly' | 'weekly'
type FormMode = 'create' | 'editEntry' | 'editRecurring'

const TITLE_PLACEHOLDER: Record<EntryTab, string> = {
  expense: 'Mat, middag, biljetter…',
  recurring: 'Hyra, internet, Spotify…',
}

const SAVE_LABEL: Record<EntryTab, string> = {
  expense: 'Lägg i loggboken',
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

/** 24050 (öre) → "240,5" for the free-text amount input (grouping-free, sv-SE decimal).
 * int64 fields arrive as number|string from the OpenAPI client, so coerce first. */
function minorToAmountStr(minor: number | string): string {
  const n = Number(minor)
  if (!n) return ''
  return (n / 100).toString().replace('.', ',')
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

interface FormState {
  type: EntryTab
  amountStr: string
  title: string
  paidBy: string | null
  splitMode: SplitMode
  vals: Record<string, string>
  wholeMemberId: string | null
  cadence: Cadence
  date: string
}

const EMPTY_STATE: FormState = {
  type: 'expense',
  amountStr: '',
  title: '',
  paidBy: null,
  splitMode: 'equal',
  vals: {},
  wholeMemberId: null,
  cadence: 'monthly',
  date: todayIso(),
}

/** Split mode the API stored maps 1:1 to the editor modes (an "Allt på en" entry reads
 * back as an ordinary Amount split — ADR-0018 note 4 — so it prefills as `amount`). */
function splitModeFrom(mode: string): SplitMode {
  return mode === 'percent' ? 'percent' : mode === 'amount' ? 'amount' : 'equal'
}

/** Reconstruct the per-member split inputs from frozen shares (no formula is stored on the
 * DTO): kr for Amount, an approximate percent for Percent. Equal needs no inputs. */
function valsFromShares(
  mode: SplitMode,
  totalMinor: number | string,
  shares: { memberId: string; shareMinor: number | string }[],
): Record<string, string> {
  const total = Number(totalMinor)
  if (mode === 'amount')
    return Object.fromEntries(shares.map((s) => [s.memberId, minorToAmountStr(s.shareMinor)]))
  if (mode === 'percent' && total > 0)
    return Object.fromEntries(
      shares.map((s) => [s.memberId, formatNum((Number(s.shareMinor) / total) * 100)]),
    )
  return {}
}

function stateFromEntry(entry: EntryDto): FormState {
  const splitMode = splitModeFrom(entry.splitMode)
  return {
    ...EMPTY_STATE,
    type: 'expense',
    amountStr: minorToAmountStr(entry.amountMinor),
    title: entry.title,
    paidBy: entry.paidByMemberId ?? null,
    splitMode,
    vals: valsFromShares(splitMode, entry.amountMinor, entry.shares),
  }
}

function stateFromRecurring(detail: RecurringDetailDto): FormState {
  const { template, shares } = detail
  const splitMode = splitModeFrom(template.splitMode)
  const payer = shares.find((s) => s.isPayer)?.memberId ?? null
  return {
    ...EMPTY_STATE,
    type: 'recurring',
    amountStr: minorToAmountStr(template.amountMinor),
    title: template.title,
    paidBy: payer,
    splitMode,
    vals: valsFromShares(splitMode, template.amountMinor, shares),
    cadence: (template.cadence as Cadence) ?? 'monthly',
    date: template.nextPostDate,
  }
}

const SHEET_TITLE: Record<FormMode, string> = {
  create: 'Ny post',
  editEntry: 'Redigera post',
  editRecurring: 'Redigera återkommande',
}

export function AddEntrySheet({
  open,
  onClose,
  editEntryId,
  editRecurringId,
}: {
  open: boolean
  onClose: () => void
  editEntryId?: string
  editRecurringId?: string
}) {
  const { householdId } = useActiveHousehold()
  const { data: me } = useMe()
  const { data: members, isLoading, isError, error, refetch } = useMembers(householdId)

  const mode: FormMode = editEntryId
    ? 'editEntry'
    : editRecurringId
      ? 'editRecurring'
      : 'create'

  const entryQuery = useEntry(open && mode === 'editEntry' ? editEntryId : undefined)
  const recurringQuery = useRecurringDetail(
    open && mode === 'editRecurring' ? editRecurringId : undefined,
  )

  const editQuery =
    mode === 'editEntry' ? entryQuery : mode === 'editRecurring' ? recurringQuery : null

  let initial: FormState = EMPTY_STATE
  if (mode === 'editEntry' && entryQuery.data) initial = stateFromEntry(entryQuery.data)
  if (mode === 'editRecurring' && recurringQuery.data)
    initial = stateFromRecurring(recurringQuery.data)

  // Remount the form when the edited target changes so its state re-initialises from `initial`.
  const formKey = editEntryId ?? editRecurringId ?? 'new'
  const loading = isLoading || (!!editQuery && editQuery.isLoading)
  const errored = isError || (!!editQuery && editQuery.isError)

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title={SHEET_TITLE[mode]}
    >
      {loading ? (
        <LoadingState rows={4} />
      ) : errored ? (
        <ErrorState error={editQuery?.error ?? error} onRetry={() => refetch()} />
      ) : (
        <EntryForm
          key={formKey}
          mode={mode}
          initial={initial}
          members={members ?? []}
          currentMemberId={me?.id}
          householdId={householdId}
          editId={editEntryId ?? editRecurringId}
          onClose={onClose}
        />
      )}
    </ResponsiveSheet>
  )
}

function EntryForm({
  mode,
  initial,
  members: memberList,
  currentMemberId,
  householdId,
  editId,
  onClose,
}: {
  mode: FormMode
  initial: FormState
  members: MemberDto[]
  currentMemberId: string | undefined
  householdId: string | undefined
  editId: string | undefined
  onClose: () => void
}) {
  const navigate = useNavigate()
  const createEntry = useCreateEntry(householdId)
  const createRecurring = useCreateRecurring(householdId)
  const updateEntry = useUpdateEntry(householdId)
  const updateRecurring = useUpdateRecurring(householdId)

  const [type, setType] = useState<EntryTab>(initial.type)
  const [amountStr, setAmountStr] = useState(initial.amountStr)
  const [title, setTitle] = useState(initial.title)
  const [paidBy, setPaidBy] = useState<string | null>(initial.paidBy)
  const [splitMode, setSplitMode] = useState<SplitMode>(initial.splitMode)
  const [vals, setVals] = useState<Record<string, string>>(initial.vals)
  const [wholeMemberId, setWholeMemberId] = useState<string | null>(initial.wholeMemberId)
  const [cadence, setCadence] = useState<Cadence>(initial.cadence)
  const [date, setDate] = useState(initial.date)

  const isEdit = mode !== 'create'
  const submitting =
    createEntry.isPending ||
    createRecurring.isPending ||
    updateEntry.isPending ||
    updateRecurring.isPending
  const totalMinor = parseAmountMinor(amountStr)

  function changeSplitMode(next: string[]) {
    const nextMode = pickSingle<SplitMode>(next, splitMode)
    setSplitMode(nextMode)
    setVals({}) // percent numbers must not be reused as kr amounts
  }

  const payer = paidBy ?? currentMemberId ?? memberList[0]?.id
  // "Allt på en": you can't owe yourself, so the picker excludes the payer and defaults to
  // the first non-payer — makes the balance-neutral payer==ower entry unreachable.
  const wholeCandidates = memberList.filter((m) => m.id !== payer)
  const wholeMember =
    wholeMemberId && wholeMemberId !== payer ? wholeMemberId : (wholeCandidates[0]?.id ?? null)
  const wholeName = memberList.find((m) => m.id === wholeMember)?.name ?? ''

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
    if (splitMode === 'whole') {
      // Syntactic sugar over an Amount split: the chosen member owes the full amount, the
      // rest owe 0 (ADR-0018 §2.1). One expense, one zero payer share — never also an IOU.
      const values: Record<string, number> = {}
      for (const m of memberList) values[m.id] = m.id === wholeMember ? totalMinor : 0
      return { mode: 'amount', values }
    }
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
    if (splitMode === 'percent' && !percentOk) {
      toast('Procenten måste bli 100')
      return
    }
    if (splitMode === 'amount' && !amountOk) {
      toast(`Delningen måste bli ${formatKr(totalMinor)}`)
      return
    }
    // Mirror the API rule: a shared expense must include someone other than the payer.
    // Equal/whole always do; only percent/kr can land the whole amount on the payer.
    if (memberList.length > 1 && (splitMode === 'percent' || splitMode === 'amount')) {
      const nonPayerShare = memberList
        .filter((m) => m.id !== payer)
        .reduce((sum, m) => sum + parseNum(vals[m.id] ?? ''), 0)
      if (nonPayerShare <= 0) {
        toast('Lägg till någon att dela med')
        return
      }
    }

    try {
      if (mode === 'editRecurring' && editId) {
        const body: UpdateRecurringRequest = {
          active: null,
          title: title.trim() || 'Utan titel',
          amountMinor: totalMinor,
          cadence,
          nextPostDate: date,
          paidByMemberId: payer ?? null,
          split: buildSplit(),
        }
        await updateRecurring.mutateAsync({ recId: editId, body })
        toast('Ändrad')
        onClose()
        return
      }

      if (mode === 'editEntry' && editId) {
        const body: UpdateEntryRequest = {
          type: 'expense',
          title: title.trim() || 'Utan titel',
          amountMinor: totalMinor,
          date: null,
          paidByMemberId: payer ?? null,
          split: buildSplit(),
          category: null,
        }
        await updateEntry.mutateAsync({ entryId: editId, body })
        toast('Ändrad')
        onClose()
        return
      }

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
        navigate({ to: '/recurring' })
        onClose()
        return
      }

      const body: CreateEntryRequest = {
        type: 'expense',
        title: title.trim() || 'Utan titel',
        amountMinor: totalMinor,
        date: null,
        paidByMemberId: payer ?? null,
        split: buildSplit(),
      }
      await createEntry.mutateAsync(body)
      toast('Tillagd i loggboken')
      onClose()
    } catch (e) {
      toast(e instanceof Error ? e.message : 'Något gick fel. Försök igen.')
    }
  }

  return (
    <div className="flex flex-col gap-5">
      {/* Type picker — only when creating; edit keeps the original type. */}
      {!isEdit && (
        <Tabs value={type} onValueChange={(v) => setType(v as EntryTab)} className="w-full">
          <TabsList className="w-full">
            <TabsTrigger value="expense">Utgift</TabsTrigger>
            <TabsTrigger value="recurring">Återkommande</TabsTrigger>
          </TabsList>
        </Tabs>
      )}

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
                  <MemberAvatar name={m.name} avatarColor={m.avatarColor} avatarEmoji={m.avatarEmoji} size="sm" />
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
              className="flex-wrap"
            >
              <ToggleGroupItem value="equal" className="flex-1">
                Lika
              </ToggleGroupItem>
              <ToggleGroupItem value="whole" className="flex-1">
                Allt
              </ToggleGroupItem>
              <ToggleGroupItem value="percent" className="flex-1">
                %
              </ToggleGroupItem>
              <ToggleGroupItem value="amount" className="flex-1">
                kr
              </ToggleGroupItem>
            </ToggleGroup>

            {splitMode === 'whole' ? (
              <div className="flex flex-col gap-2">
                <Label className={FIELD_LABEL}>Vem står för hela beloppet?</Label>
                <ToggleGroup
                  value={wholeMember ? [wholeMember] : []}
                  onValueChange={(v) => setWholeMemberId(pickSingle(v, wholeMember ?? ''))}
                  variant="outline"
                  className="flex-wrap"
                >
                  {wholeCandidates.map((m) => (
                    <ToggleGroupItem key={m.id} value={m.id} className="gap-2">
                      <MemberAvatar name={m.name} avatarColor={m.avatarColor} avatarEmoji={m.avatarEmoji} size="sm" />
                      {m.name}
                    </ToggleGroupItem>
                  ))}
                </ToggleGroup>
                <p className="text-right text-xs font-semibold text-muted-foreground">
                  {totalMinor > 0 && wholeName
                    ? `${wholeName} står för hela beloppet`
                    : 'En person står för hela beloppet'}
                </p>
              </div>
            ) : (
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
                      <MemberAvatar name={m.name} avatarColor={m.avatarColor} avatarEmoji={m.avatarEmoji} size="sm" />
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
            )}
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
                Settl bokför den i loggboken varje period och delar om automatiskt. Ställ
                in en gång, glöm den sen.
              </p>
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
        {isEdit ? 'Spara ändringar' : SAVE_LABEL[type]}
      </Button>
    </div>
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
