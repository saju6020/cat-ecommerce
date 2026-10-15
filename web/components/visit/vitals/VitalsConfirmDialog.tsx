'use client';

import { useRef } from 'react';
import { TriangleAlert } from 'lucide-react';
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
import { vitalSignLabel } from '@/lib/format/clinical';
import type { OutOfRangeSign } from '@/lib/api/visits';

/**
 * `VitalsConfirmDialog` — HO-EMR-002 §11.2, ADR-FE-052 (US-CLN-11 AC2). On the server's
 * `400 VitalsOutsidePlausibleRange` it lists EACH value with the range it was judged against and the
 * unit; *Confirm and save* re-posts with `confirmedOutOfRange`; *Correct values* returns focus to the
 * first listed field. The warning that counts is the one the server recorded, so this is driven by the
 * response, not by the early hint. (`VitalSignOutOfRange` is a field error and never reaches here.)
 */
export function VitalsConfirmDialog({
  open,
  items,
  pending,
  error,
  onConfirm,
  onCorrect,
}: {
  open: boolean;
  items: OutOfRangeSign[];
  pending: boolean;
  error: string | null;
  onConfirm: () => void;
  onCorrect: () => void;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  // Correct values (or Esc) moves focus to the first listed field, so the dialog must NOT also hand
  // focus back to the Save button it was opened from.
  const correcting = useRef(false);
  const correct = () => {
    correcting.current = true;
    onCorrect();
  };
  return (
    <Dialog open={open} onOpenChange={(next) => !next && !pending && correct()}>
      <DialogContent
        returnFocusRef={returnFocusRef}
        onCloseAutoFocus={(event) => {
          if (correcting.current) event.preventDefault();
          correcting.current = false;
        }}
        hideClose
        className="max-h-[90vh] overflow-y-auto"
      >
        <DialogHeader>
          <DialogTitle>These values are outside the usual range</DialogTitle>
          <DialogDescription>
            Check each one. If they are correct, confirm to save them anyway — the confirmation is
            recorded with the entry.
          </DialogDescription>
        </DialogHeader>
        <ul className="m-0 mb-3 flex list-none flex-col gap-2 p-0">
          {items.map((item) => (
            <li
              key={item.sign}
              className="flex items-start gap-2 rounded-[var(--radius-sm)] bg-warning-bg p-2.5 text-sm text-warning-ink"
            >
              <TriangleAlert size={16} className="mt-0.5 flex-none" aria-hidden="true" />
              <span>
                <strong>{vitalSignLabel(item.sign)}</strong> {item.value} {item.unit} — usual range{' '}
                {item.plausibleMin}–{item.plausibleMax} {item.unit}
              </span>
            </li>
          ))}
        </ul>
        {error && (
          <p role="alert" className="mb-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={correct} disabled={pending}>
            Correct values
          </Button>
          <Button onClick={onConfirm} disabled={pending} aria-busy={pending}>
            {pending ? 'Saving…' : 'Confirm and save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
