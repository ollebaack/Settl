/**
 * Välj profilbild — the avatar emoji picker (profile-addendum §2.2, frame 3).
 * A responsive sheet with a live preview, keyword search, and a curated emoji grid
 * (no emoji-library dependency — see lib/emoji.ts for the why). Committing a pick or
 * "use my letter" is handed back to the profile page via `onCommit`, which persists it
 * through PUT /me; the API validates the value (ADR-0006/0019), not this picker.
 */
import { useEffect, useState } from 'react'
import { ResponsiveSheet } from '@/components/responsive-sheet'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { MemberAvatar } from '@/components/member-avatar'
import { searchEmojis } from '@/lib/emoji'
import { cn } from '@/lib/utils'

export function AvatarPickerSheet({
  open,
  onClose,
  name,
  avatarColor,
  currentEmoji,
  busy = false,
  onCommit,
}: {
  open: boolean
  onClose: () => void
  name: string
  avatarColor: string
  currentEmoji: string | null
  busy?: boolean
  /** null = reset to the letter initial. */
  onCommit: (emoji: string | null) => void
}) {
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<string | null>(currentEmoji)

  // Re-sync the highlighted cell + clear the search each time the sheet opens, so it
  // reflects the member's current emoji rather than a stale pick from a previous open.
  useEffect(() => {
    if (open) {
      setSelected(currentEmoji)
      setQuery('')
    }
  }, [open, currentEmoji])

  const results = searchEmojis(query)

  return (
    <ResponsiveSheet
      open={open}
      onOpenChange={(o) => {
        if (!o) onClose()
      }}
      title="Välj profilbild"
      description="Plocka en emoji — eller håll dig till din bokstav."
    >
      <div className="flex flex-col gap-4">
        <div className="flex justify-center py-1">
          <MemberAvatar
            name={name}
            avatarColor={avatarColor}
            avatarEmoji={selected}
            size="xl"
            className="ring-4 ring-background"
          />
        </div>

        <Input
          type="search"
          aria-label="Sök emoji"
          placeholder="Sök emoji"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />

        {results.length > 0 ? (
          <div className="grid max-h-[240px] grid-cols-6 gap-1.5 overflow-y-auto">
            {results.map((e) => {
              const on = e.emoji === selected
              return (
                <button
                  key={e.emoji}
                  type="button"
                  aria-label={e.keywords[0] ?? e.emoji}
                  aria-pressed={on}
                  onClick={() => setSelected(e.emoji)}
                  className={cn(
                    'grid aspect-square place-items-center rounded-xl bg-muted text-[22px] leading-none outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring',
                    on ? 'bg-accent ring-2 ring-inset ring-primary' : 'hover:bg-accent/60',
                  )}
                >
                  {e.emoji}
                </button>
              )
            })}
          </div>
        ) : (
          <p className="py-4 text-center text-sm text-muted-foreground">
            Inga emojis matchar ”{query}”.
          </p>
        )}

        <div className="flex flex-col gap-2 pt-1">
          <Button
            type="button"
            disabled={!selected || busy}
            onClick={() => selected && onCommit(selected)}
          >
            {selected ? `Använd ${selected}` : 'Använd emoji'}
          </Button>
          <Button type="button" variant="outline" disabled={busy} onClick={() => onCommit(null)}>
            Använd min bokstav istället
          </Button>
        </div>
      </div>
    </ResponsiveSheet>
  )
}
