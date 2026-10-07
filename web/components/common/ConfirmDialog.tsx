'use client';

import * as React from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

/**
 * `components/common/ConfirmDialog.tsx` — HO-CAT-002 §11.7, UI-3.
 *
 * "Takes an explicit consequence description. No generic 'Are you sure?'" — `consequence` is a
 * required prop for exactly that reason: it is a compile error to render this without stating
 * what the action does (MarkDeceasedDialog, TransferOwnershipDialog, the schedule-price dialog,
 * the maintenance switch, …all build on this rather than a second confirm implementation).
 *
 * Built on `components/ui/dialog.tsx`'s Radix primitive: `Escape` closes for free, and focus
 * returns to whatever opened it via `useDialogReturnFocus` (HO-CAT-002 §8.5 — component test
 * U28).
 */
export interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** The dialog's heading — the action being confirmed, e.g. "Mark Whiskers as deceased". */
  title: string;
  /** The explicit, plain-language statement of what this action does. Required — UI-3. */
  consequence: React.ReactNode;
  /** Runs on confirm. May be async; the dialog disables its buttons and shows `pendingLabel`
   *  while it is in flight, and stays open until it resolves so a rejection can be shown. */
  onConfirm: () => void | Promise<void>;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Shown on the confirm button while `onConfirm`'s promise is pending. */
  pendingLabel?: string;
  /** `destructive` for an action that removes access or reverses something (transfer ownership,
   *  clear high-risk, cancel); the default `primary` styling otherwise (mark deceased, publish). */
  variant?: 'primary' | 'destructive';
}

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  consequence,
  onConfirm,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  pendingLabel = 'Working…',
  variant = 'primary',
}: ConfirmDialogProps) {
  const [isPending, setIsPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(open);

  async function handleConfirm() {
    setIsPending(true);
    try {
      await onConfirm();
    } finally {
      setIsPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !isPending && onOpenChange(next)}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{consequence}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={isPending}>
            {cancelLabel}
          </Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'primary'}
            onClick={handleConfirm}
            disabled={isPending}
            aria-busy={isPending}
          >
            {isPending ? pendingLabel : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
