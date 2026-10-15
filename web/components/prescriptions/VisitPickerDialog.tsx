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
import { TextField } from '@/components/inventory/fields';
import { useOpenVisits } from '@/lib/hooks/useVisit';
import { formatInstantInZone } from '@/lib/format/pharmacy';
import { DEFAULT_BRANCH_TIME_ZONE } from '@/lib/hooks/useBranchTimeZone';
import { isGuid } from '@/lib/validation/guid';

/**
 * `VisitPickerDialog` — HO-PHM-002 OQ-5. A prescription or diet authorisation is written from an OPEN
 * visit. HO-EMR-001 has no "this cat's open visits" read, but it does list the branch's open visits:
 * the cat's own are shown here to choose from, and a visit reference can still be pasted (the deep
 * link HO-EMR-002 will use, or a visit opened at another desk). Nothing is invented: the visit is
 * checked by the server when the prescription is issued.
 */
export function VisitPickerDialog({
  open,
  onOpenChange,
  catId,
  catName,
  purpose,
  onChoose,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catId: string;
  catName: string;
  /** What is being written, for the title: "prescription" or "diet authorisation". */
  purpose: string;
  onChoose: (visitId: string) => void;
}) {
  const visits = useOpenVisits();
  const [pasted, setPasted] = React.useState('');
  const [error, setError] = React.useState<string | undefined>();
  const [selected, setSelected] = React.useState('');
  const returnFocusRef = useDialogReturnFocus(open);
  const mine = (visits.data ?? []).filter((visit) => visit.cat.id === catId);

  React.useEffect(() => {
    if (!open) {
      setPasted('');
      setSelected('');
      setError(undefined);
    }
  }, [open]);

  function choose() {
    const reference = pasted.trim() || selected;
    if (!reference) {
      setError('Choose a visit or paste a visit reference.');
      return;
    }
    if (!isGuid(reference)) {
      setError('That does not look like a visit reference.');
      return;
    }
    onChoose(reference);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <DialogHeader>
          <DialogTitle>Which visit is this {purpose} for?</DialogTitle>
          <DialogDescription>
            A {purpose} is written from an open visit. Choose one of {catName}’s open visits, or
            paste a visit reference.
          </DialogDescription>
        </DialogHeader>
        {visits.isLoading ? (
          <p role="status" className="text-sm text-ink-muted">
            Looking for {catName}’s open visits…
          </p>
        ) : mine.length > 0 ? (
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 text-sm font-semibold text-ink">Open visits</legend>
            {mine.map((visit) => (
              <label
                key={visit.id}
                className="flex cursor-pointer items-start gap-2 rounded-[var(--radius-sm)] border border-border p-2 text-sm"
              >
                <input
                  type="radio"
                  name="visit"
                  value={visit.id}
                  checked={selected === visit.id}
                  onChange={() => {
                    setSelected(visit.id);
                    setError(undefined);
                  }}
                  className="mt-0.5"
                />
                <span>
                  <b>{visit.service}</b>
                  <span className="block text-xs text-ink-muted">
                    Opened {formatInstantInZone(visit.openedOn, DEFAULT_BRANCH_TIME_ZONE)}
                  </span>
                </span>
              </label>
            ))}
          </fieldset>
        ) : (
          <p className="text-sm text-ink-muted">
            {visits.isError
              ? 'The open visits could not be loaded. Paste the visit reference instead.'
              : `${catName} has no open visit at this branch. Open the visit first, or paste a reference below.`}
          </p>
        )}
        <div className="mt-3">
          <TextField
            id="visit-reference"
            label="Or paste a visit reference"
            value={pasted}
            autoComplete="off"
            onChange={(event) => {
              setPasted(event.target.value);
              setError(undefined);
            }}
            error={error}
          />
        </div>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={choose}>Continue</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
