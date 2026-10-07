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
import { getViolationCode, describeAppointmentError } from '@/lib/api/errors';
import type { SlotPerformer } from '@/lib/api/booking';

/**
 * `components/booking/WaitlistJoinDialog.tsx` — HO-APT-002 §11.1, US-CLN-09. Cat, service and date
 * are shown prefilled; the preferred vet is optional. The claim window's length comes from the join
 * response (`claimWindowMinutes`), so it is stated after joining, not guessed beforehand.
 * `SlotsStillAvailable` closes the dialog through `onSlotsAvailable` so the container can refetch
 * and say "Times have just become available — pick one below."
 */
export function WaitlistJoinDialog({
  open,
  onOpenChange,
  catName,
  serviceName,
  dateLabel,
  performers,
  defaultPerformerId,
  onJoin,
  onSlotsAvailable,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  serviceName: string;
  dateLabel: string;
  performers: SlotPerformer[];
  defaultPerformerId: string;
  onJoin: (preferredPerformerId: string | undefined) => Promise<{ claimWindowMinutes: number }>;
  onSlotsAvailable: () => void;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [performerId, setPerformerId] = useState(defaultPerformerId);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [claimWindow, setClaimWindow] = useState<number | null>(null);

  function close(next: boolean) {
    onOpenChange(next);
    if (!next) {
      setError(null);
      setClaimWindow(null);
    }
  }

  async function join() {
    setPending(true);
    setError(null);
    try {
      const result = await onJoin(performerId || undefined);
      setClaimWindow(result.claimWindowMinutes);
    } catch (caught) {
      if (getViolationCode(caught) === 'SlotsStillAvailable') {
        close(false);
        onSlotsAvailable();
      } else {
        setError(describeAppointmentError(caught));
      }
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && close(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>
            {claimWindow === null ? 'Join the waitlist' : 'You’re on the waitlist'}
          </DialogTitle>
          <DialogDescription>
            {claimWindow === null
              ? 'If a time frees up we’ll send you a message, and you’ll have a short window to claim it.'
              : `If a time frees up we’ll send you a message. You’ll have ${claimWindow} minutes to claim it.`}
          </DialogDescription>
        </DialogHeader>

        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
          <dt className="font-semibold text-ink">Cat</dt>
          <dd className="text-ink-muted">{catName}</dd>
          <dt className="font-semibold text-ink">Service</dt>
          <dd className="text-ink-muted">{serviceName}</dd>
          <dt className="font-semibold text-ink">Day</dt>
          <dd className="text-ink-muted">{dateLabel}</dd>
        </dl>

        {claimWindow === null && (
          <div className="mt-4 flex flex-col gap-1.5">
            <label htmlFor="waitlist-vet" className="text-sm font-semibold text-ink">
              Preferred vet <span className="font-normal text-ink-muted">(optional)</span>
            </label>
            <select
              id="waitlist-vet"
              value={performerId}
              onChange={(event) => setPerformerId(event.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            >
              <option value="">No preference</option>
              {performers.map((performer) => (
                <option key={performer.id} value={performer.id}>
                  {performer.name}
                </option>
              ))}
            </select>
          </div>
        )}

        {error && (
          <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}

        <DialogFooter>
          {claimWindow === null ? (
            <>
              <Button variant="secondary" onClick={() => close(false)} disabled={pending}>
                Cancel
              </Button>
              <Button onClick={join} disabled={pending} aria-busy={pending}>
                {pending ? 'Joining…' : 'Join the waitlist'}
              </Button>
            </>
          ) : (
            <Button onClick={() => close(false)}>Done</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
