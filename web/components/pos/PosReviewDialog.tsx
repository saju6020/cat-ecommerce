'use client';

import { useRef } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';

/**
 * `components/pos/PosReviewDialog.tsx` — HO-ORD-002 §11.11, UI-3. The last look before a counter sale is
 * recorded: it restates the total (the server's quote) and the payments as typed. Unlike a generic
 * confirm, **Complete sale is focused when it opens**, so the keyboard path (amount, Enter, Enter) works
 * without reaching for the mouse. Closing returns focus to where it was.
 */
export function PosReviewDialog({
  open,
  onOpenChange,
  summary,
  pending,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  summary: string;
  pending: boolean;
  onConfirm: () => void;
}) {
  const confirmRef = useRef<HTMLButtonElement>(null);
  const returnFocusRef = useDialogReturnFocus(open);
  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent
        hideClose
        returnFocusRef={returnFocusRef}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          confirmRef.current?.focus();
        }}
      >
        <DialogHeader>
          <DialogTitle>Complete this sale?</DialogTitle>
          <DialogDescription>{summary}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Back
          </Button>
          <Button ref={confirmRef} onClick={onConfirm} disabled={pending} aria-busy={pending}>
            {pending ? 'Recording…' : 'Complete sale'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
