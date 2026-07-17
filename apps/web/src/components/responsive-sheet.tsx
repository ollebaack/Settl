/**
 * One overlay wrapper that is a centered Dialog on desktop (≥980px) and a
 * bottom Drawer (Base UI, slide-up + grip handle) on mobile, sharing the same
 * content. Implementation-map §3. Sheets are driven by URL search params via
 * SheetRouter; this component just renders the right shell for the viewport.
 */
import type { ReactNode } from 'react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer'
import { cn } from '@/lib/utils'
import { useIsWide } from '@/lib/use-media'

export interface ResponsiveSheetProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: ReactNode
  description?: ReactNode
  children?: ReactNode
  className?: string
}

export function ResponsiveSheet({
  open,
  onOpenChange,
  title,
  description,
  children,
  className,
}: ResponsiveSheetProps) {
  const wide = useIsWide()

  if (wide) {
    return (
      <Dialog open={open} onOpenChange={(o) => onOpenChange(o)}>
        {/* Cap the dialog at the viewport and scroll the body — otherwise a tall sheet (e.g. the
            household switcher with many books, or a long settle-up) overflows the centered popup
            and its bottom actions land outside the viewport, unreachable. Mirrors the mobile
            drawer's scrollable body. */}
        <DialogContent className={cn('flex max-h-[85dvh] flex-col sm:max-w-[480px]', className)}>
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            {description ? (
              <DialogDescription>{description}</DialogDescription>
            ) : (
              <DialogDescription className="sr-only">{title}</DialogDescription>
            )}
          </DialogHeader>
          <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto">{children}</div>
        </DialogContent>
      </Dialog>
    )
  }

  return (
    <Drawer open={open} onOpenChange={(o) => onOpenChange(o)} showSwipeHandle>
      <DrawerContent className={className}>
        <DrawerHeader>
          <DrawerTitle>{title}</DrawerTitle>
          {description ? (
            <DrawerDescription>{description}</DrawerDescription>
          ) : (
            <DrawerDescription className="sr-only">{title}</DrawerDescription>
          )}
        </DrawerHeader>
        <div className="min-h-0 flex-1 overflow-x-hidden overflow-y-auto px-4 pt-1 pb-6">
          {children}
        </div>
      </DrawerContent>
    </Drawer>
  )
}
