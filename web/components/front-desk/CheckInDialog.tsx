'use client';

import { useState } from 'react';
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
import { describeAppointmentError } from '@/lib/api/errors';
import type { TriagePriority } from '@/lib/api/types';
import { PRIORITY_OPTIONS } from './PriorityBadge';

/**
 * `components/front-desk/CheckInDialog.tsx` — HO-APT-002 §11.5: check a booked cat in, with an
 * optional triage choice (Routine by default). The choice is a radio group of words, not colours.
 * A server refusal (`CheckInNotToday`, `InvalidAppointmentTransition`) is shown inside the dialog.
 */
export function CheckInDialog({
  open,
  onOpenChange,
  catName,
  customerName,
  onCheckIn,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  customerName: string;
  onCheckIn: (priority: TriagePriority) => Promise<void>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [priority, setPriority] = useState<TriagePriority>('Routine');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setPending(true);
    setError(null);
    try {
      await onCheckIn(priority);
      onOpenChange(false);
      setPriority('Routine');
    } catch (caught) {
      setError(describeAppointmentError(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Check in {catName}</DialogTitle>
          <DialogDescription>
            {customerName}’s cat joins the queue once checked in. Choose how urgent the case is, if
            it isn’t routine.
          </DialogDescription>
        </DialogHeader>
        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-sm font-semibold text-ink">Priority</legend>
          {PRIORITY_OPTIONS.map((option) => (
            <label key={option.value} className="flex items-start gap-2 text-sm text-ink">
              <input
                type="radio"
                name="checkin-priority"
                value={option.value}
                checked={priority === option.value}
                onChange={() => setPriority(option.value)}
                className="mt-0.5"
              />
              <span>
                <span className="font-semibold">{option.label}</span>
                <span className="text-ink-muted"> — {option.hint}</span>
              </span>
            </label>
          ))}
        </fieldset>
        {error && (
          <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={pending} aria-busy={pending}>
            {pending ? 'Checking in…' : 'Check in'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
