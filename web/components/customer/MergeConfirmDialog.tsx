'use client';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { NotYetRecordedNotice } from './NotYetRecordedNotice';
import type { StaffCustomer } from '@/lib/api/staff-customers';

/**
 * `components/customer/MergeConfirmDialog.tsx` — split out of `MergeReview.tsx` (ARCH gate LOW
 * "component size", first pass) purely to keep that file under HO-ACC-002 §11's ~150-line
 * guideline once its own hooks moved out to the page — this dialog's content and behaviour are
 * unchanged, still driven entirely by props from `MergeReview`.
 *
 * CR MEDIUM 2 (first-pass gate fix round, item 13) — lists cats by name, not just a count: a fresh
 * `GET /api/staff/customers/{id}` (Q-H1) now carries the actual list (`StaffCustomer.cats`), so
 * staff confirming a merge can see exactly which cats are about to move, not only how many.
 */
export interface MergeConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  returnFocusRef: React.RefObject<HTMLElement | null>;
  survivor: StaffCustomer | undefined;
  others: StaffCustomer[];
  isPending: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function MergeConfirmDialog({
  open,
  onOpenChange,
  returnFocusRef,
  survivor,
  others,
  isPending,
  onCancel,
  onConfirm,
}: MergeConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Merge these records?</DialogTitle>
          <DialogDescription>
            {survivor?.fullName} will be kept. {others.map((o) => o.fullName).join(', ')} will be
            retired.
          </DialogDescription>
        </DialogHeader>
        <ul className="flex flex-col gap-1 text-sm text-ink">
          {others.map((o) =>
            o.cats.length === 0 ? (
              <li key={o.id}>No cats will move from {o.fullName}.</li>
            ) : (
              <li key={o.id}>
                {o.cats.map((cat) => cat.name).join(', ')} will move from {o.fullName}.
              </li>
            ),
          )}
        </ul>
        <NotYetRecordedNotice />
        <DialogFooter>
          <Button variant="secondary" onClick={onCancel}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            disabled={isPending}
            aria-busy={isPending}
            onClick={onConfirm}
          >
            Merge
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
