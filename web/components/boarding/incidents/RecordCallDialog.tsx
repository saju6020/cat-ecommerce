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
import { FormErrorAlert, TextAreaRow } from '@/components/boarding/shared/FormBits';
import { describeBoardingError } from '@/lib/api/errors';
import { useRecordIncidentPhoneCall } from '@/lib/hooks/useBoardingCare';

/**
 * `components/boarding/incidents/RecordCallDialog.tsx` — HO-BRD-002 §11.5/§11.9, US-BRD-13 AC3:
 * **Record call** captures the outcome (required) and whether the owner was reached (which counts as
 * acknowledgement). It owns what is typed, so the dashboard's 30 s refresh never clears it
 * (ADR-FE-101, U-C2).
 */
export function RecordCallDialog({
  open,
  onOpenChange,
  incidentId,
  customerName,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  incidentId: string;
  customerName: string;
}) {
  const record = useRecordIncidentPhoneCall();
  const [outcome, setOutcome] = useState('');
  const [reached, setReached] = useState(true);
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [error, setError] = useState<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  async function submit() {
    if (!outcome.trim()) {
      setFieldError('Enter what happened on the call.');
      return;
    }
    setFieldError(undefined);
    setError(null);
    try {
      await record.mutateAsync({ incidentId, outcome: outcome.trim(), ownerReached: reached });
      setOutcome('');
      onOpenChange(false);
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Record the call to {customerName}</DialogTitle>
          <DialogDescription>This closes the follow-up for this incident.</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3">
          <TextAreaRow
            id="call-outcome"
            label="What happened on the call"
            required
            value={outcome}
            error={fieldError}
            onChange={(e) => setOutcome(e.target.value)}
          />
          <label className="flex items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              checked={reached}
              onChange={(e) => setReached(e.target.checked)}
            />
            I spoke to the owner (counts as acknowledged)
          </label>
          <FormErrorAlert message={error} />
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => void submit()} disabled={record.isPending}>
            {record.isPending ? 'Saving…' : 'Record call'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
