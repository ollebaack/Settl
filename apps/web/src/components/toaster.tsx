/**
 * The single app Toaster (Sonner), mounted once in the root layout. Bottom-
 * centre, short auto-dismiss; extra bottom offset on mobile clears the tab bar.
 * Emit toasts from anywhere with `toast(...)` imported from 'sonner'.
 */
import { Toaster as UiToaster } from '@/components/ui/sonner'

export function Toaster() {
  return (
    <UiToaster
      position="bottom-center"
      duration={2600}
      offset={{ bottom: 28 }}
      mobileOffset={{ bottom: 104 }}
    />
  )
}
