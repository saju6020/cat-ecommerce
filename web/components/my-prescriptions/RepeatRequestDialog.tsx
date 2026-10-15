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
import { SelectField, TextAreaField } from '@/components/inventory/fields';
import { describePharmacyError } from '@/lib/api/errors';

/**
 * `RepeatRequestDialog` — HO-PHM-002 §11.8 (US-PHM-06 AC1). The owner chooses the branch to collect from
 * and may add a note, then confirms. It only opens when the server said `canRequest` — the dialog does
 * not decide eligibility. A refusal at send time (the window closed while the dialog was open) is shown
 * inside the dialog in the server's words. No price is shown or taken: a repeat is paid for at collection.
 */
export function RepeatRequestDialog({
  open,
  onOpenChange,
  medicineName,
  branches,
  onRequest,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  medicineName: string;
  branches: Array<{ id: string; name: string }>;
  onRequest: (input: { preferredBranchId: string; note: string | null }) => Promise<unknown>;
}) {
  const [branchId, setBranchId] = React.useState('');
  const [note, setNote] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [fieldError, setFieldError] = React.useState<string | undefined>();
  const [pending, setPending] = React.useState(false);
  const returnFocusRef = useDialogReturnFocus(open);

  React.useEffect(() => {
    if (!open) {
      setNote('');
      setError(null);
      setFieldError(undefined);
    } else if (!branchId && branches.length === 1) {
      setBranchId(branches[0]!.id);
    }
  }, [open, branches, branchId]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!branchId) {
      setFieldError('Choose where you will collect it.');
      return;
    }
    setFieldError(undefined);
    setError(null);
    setPending(true);
    try {
      await onRequest({ preferredBranchId: branchId, note: note.trim() || null });
      onOpenChange(false);
    } catch (failure) {
      setError(describePharmacyError(failure));
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <form onSubmit={submit} noValidate>
          <DialogHeader>
            <DialogTitle>Request a repeat of {medicineName}</DialogTitle>
            <DialogDescription>
              Choose where to collect it. We will message you when it is ready. You pay at
              collection.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-3">
            <SelectField
              id="rr-branch"
              label="Collect from"
              required
              value={branchId}
              error={fieldError}
              onChange={(event) => setBranchId(event.target.value)}
            >
              <option value="">Choose a branch</option>
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </SelectField>
            <TextAreaField
              id="rr-note"
              label="Note for the pharmacy (optional)"
              rows={3}
              maxLength={500}
              value={note}
              onChange={(event) => setNote(event.target.value)}
            />
            {error && (
              <p role="alert" className="text-sm font-semibold text-danger-ink">
                {error}
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
              Cancel
            </Button>
            <Button type="submit" disabled={pending} aria-busy={pending}>
              {pending ? 'Sending…' : 'Request repeat'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
