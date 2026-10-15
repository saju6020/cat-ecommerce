'use client';

import { useEffect, useState } from 'react';
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
import { describeSurgeryError } from '@/lib/api/errors';
import { SURGERY_LIMITS, overrideReasonSchema } from '@/lib/validation/surgery';
import { TextAreaBox } from './TextAreaBox';

/**
 * ⭐ `ConsentOverrideDialog` — HO-SRG-002 §11.3, ADR-FE-072, BR-5. Two steps. First the reason (10 to 1000
 * characters, with a counter; **Continue** stays disabled below the minimum). Then a final confirm that
 * NAMES the vet and says the override is recorded permanently. The typed reason lives in this component's
 * own state, so a 30 s poll of the surgery never clears it (U-B6). The server decides whether the override
 * is allowed; this dialog only collects what it needs.
 */
export function ConsentOverrideDialog({
  open,
  onOpenChange,
  vetName,
  catName,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  vetName: string;
  catName: string;
  onConfirm: (reason: string) => Promise<unknown>;
}) {
  const [reason, setReason] = useState('');
  const [step, setStep] = useState<'reason' | 'confirm'>('reason');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const returnFocusRef = useDialogReturnFocus(open);

  useEffect(() => {
    if (!open) {
      setReason('');
      setStep('reason');
      setError(null);
    }
  }, [open]);

  const valid = overrideReasonSchema.safeParse(reason).success;

  async function confirm() {
    setPending(true);
    setError(null);
    try {
      await onConfirm(reason.trim());
      onOpenChange(false);
    } catch (failure) {
      setError(describeSurgeryError(failure));
      setStep('reason');
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent hideClose returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Start without recorded consent</DialogTitle>
          <DialogDescription>
            {step === 'reason'
              ? `Only for an emergency. Say why ${catName}’s surgery cannot wait for consent.`
              : `${vetName}, you are starting ${catName}’s surgery without recorded consent. This override is recorded permanently with your name and your reason.`}
          </DialogDescription>
        </DialogHeader>
        {step === 'reason' ? (
          <TextAreaBox
            id="override-reason"
            label="Reason for the emergency override"
            value={reason}
            onChange={setReason}
            max={SURGERY_LIMITS.overrideReasonMax}
            min={SURGERY_LIMITS.overrideReasonMin}
            error={error ?? undefined}
          />
        ) : (
          <blockquote className="rounded-[var(--radius-sm)] border-l-4 border-danger-ink bg-surface-sunken p-3 text-sm text-ink">
            {reason.trim()}
          </blockquote>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          {step === 'reason' ? (
            <Button disabled={!valid} onClick={() => setStep('confirm')}>
              Continue
            </Button>
          ) : (
            <>
              <Button variant="secondary" onClick={() => setStep('reason')} disabled={pending}>
                Back
              </Button>
              <Button variant="destructive" onClick={() => void confirm()} disabled={pending}>
                {pending ? 'Starting…' : 'Start surgery without consent'}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
