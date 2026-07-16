/**
 * Confirmation dialog for destructive / irreversible actions (ledger-editing addendum
 * §2.3–2.4). A domain component composed from the shadcn Dialog primitive (Base UI);
 * always rendered centered regardless of viewport so it reads as an interruption over
 * whatever sheet triggered it. Copy is passed in by the caller.
 */
import type { ReactNode } from 'react'
import { Loader2Icon } from 'lucide-react'

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  children,
  confirmLabel,
  cancelLabel = 'Avbryt',
  secondaryLabel,
  onSecondary,
  destructive = false,
  busy = false,
  onConfirm,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: ReactNode
  description?: ReactNode
  children?: ReactNode
  confirmLabel: string
  cancelLabel?: string
  /** Optional middle action (e.g. "Pausa i stället") offered alongside confirm/cancel. */
  secondaryLabel?: string
  onSecondary?: () => void
  destructive?: boolean
  busy?: boolean
  onConfirm: () => void
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent showCloseButton={false} className="gap-4">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description ? (
            <DialogDescription>{description}</DialogDescription>
          ) : (
            <DialogDescription className="sr-only">{title}</DialogDescription>
          )}
        </DialogHeader>
        {children}
        <DialogFooter>
          <Button variant="outline" disabled={busy} onClick={() => onOpenChange(false)}>
            {cancelLabel}
          </Button>
          {secondaryLabel && onSecondary && (
            <Button variant="secondary" disabled={busy} onClick={onSecondary}>
              {secondaryLabel}
            </Button>
          )}
          <Button
            variant={destructive ? 'destructive' : 'default'}
            disabled={busy}
            onClick={onConfirm}
          >
            {busy && <Loader2Icon className="animate-spin" />}
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** Shared warnbox used inside confirm dialogs — a tinted attention block with a heading
 * and a body line (addendum copy). */
export function WarnBox({ heading, body }: { heading: string; body: ReactNode }) {
  return (
    <div className="flex flex-col gap-1 rounded-2xl bg-destructive/10 px-4 py-3 text-sm text-destructive">
      <span className="font-semibold">{heading}</span>
      <span className="text-destructive/80">{body}</span>
    </div>
  )
}
