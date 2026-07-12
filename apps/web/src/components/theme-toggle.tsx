/**
 * Light/dark toggle. Theme follows the system by default and is persisted;
 * next-themes toggles the `.dark` class on <html> (and drives the Sonner theme).
 * Placed in the sidebar footer (desktop) / settings area (mobile).
 */
import { useTheme } from 'next-themes'
import { MoonIcon, SunIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'

export function ThemeToggle({ className }: { className?: string }) {
  const { resolvedTheme, setTheme } = useTheme()
  const isDark = resolvedTheme === 'dark'
  return (
    <Button
      variant="ghost"
      size="icon-sm"
      className={className}
      aria-label={isDark ? 'Byt till ljust läge' : 'Byt till mörkt läge'}
      onClick={() => setTheme(isDark ? 'light' : 'dark')}
    >
      {isDark ? <SunIcon /> : <MoonIcon />}
    </Button>
  )
}
