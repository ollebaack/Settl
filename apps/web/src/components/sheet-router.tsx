/**
 * Reads the `sheet` (+ id/person) search param on the active route and renders
 * the matching overlay. Only one open at a time; the underlying screen stays
 * mounted. Mounted once in the root layout. Phase 5B agents flesh out each
 * sheet behind its stable import path.
 */
import { useSheet } from '@/lib/sheet'
import { AddEntrySheet } from '@/components/sheets/add-entry-sheet'
import { EntryDetailSheet } from '@/components/sheets/entry-detail-sheet'
import { SettleUpSheet } from '@/components/sheets/settle-up-sheet'
import { HouseholdSwitcherSheet } from '@/components/sheets/household-switcher-sheet'
import { NewHouseholdSheet } from '@/components/sheets/new-household-sheet'
import { RecurringDetailSheet } from '@/components/sheets/recurring-detail-sheet'
import { LeaveHouseholdSheet } from '@/components/sheets/leave-household-sheet'
import { TransferOwnershipSheet } from '@/components/sheets/transfer-ownership-sheet'
import { ArchiveHouseholdSheet } from '@/components/sheets/archive-household-sheet'

export function SheetRouter() {
  const { sheet, id, person, closeSheet } = useSheet()

  return (
    <>
      <AddEntrySheet
        open={sheet === 'add' || sheet === 'edit' || sheet === 'editRecurring'}
        onClose={closeSheet}
        editEntryId={sheet === 'edit' ? id : undefined}
        editRecurringId={sheet === 'editRecurring' ? id : undefined}
      />
      <EntryDetailSheet open={sheet === 'entry'} onClose={closeSheet} entryId={id} />
      <SettleUpSheet open={sheet === 'settle'} onClose={closeSheet} person={person} />
      <HouseholdSwitcherSheet open={sheet === 'households'} onClose={closeSheet} />
      <NewHouseholdSheet open={sheet === 'newHousehold'} onClose={closeSheet} />
      <RecurringDetailSheet open={sheet === 'recurring'} onClose={closeSheet} recId={id} />
      <LeaveHouseholdSheet open={sheet === 'leaveHousehold'} onClose={closeSheet} householdId={id} />
      <TransferOwnershipSheet open={sheet === 'transferOwnership'} onClose={closeSheet} householdId={id} />
      <ArchiveHouseholdSheet open={sheet === 'archiveHousehold'} onClose={closeSheet} householdId={id} />
    </>
  )
}
