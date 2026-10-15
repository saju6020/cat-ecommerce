'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { ApiValidationError } from '@/lib/api/client';
import { describeSurgeryError, surgeryViolations } from '@/lib/api/errors';
import { formatDateOnly } from '@/lib/format/time';
import { followUpLabel } from '@/lib/format/surgery';
import { useDischargeSurgery } from '@/lib/hooks/useSurgeries';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import type { DischargeDraft, SurgeryDetail, SurgeryFollowUpKind } from '@/lib/api/surgeries';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass, hintClass, inputClass, labelClass } from '@/components/surgery/styles';

interface Row {
  kind: SurgeryFollowUpKind;
  dueOn: string;
  windowFrom: string;
  windowTo: string;
}

/** A date outside the draft's window is caught before submit (the draft carries the window). */
export function followUpDateError(row: Row): string | null {
  if (!row.dueOn) return 'Choose a date.';
  if (row.dueOn < row.windowFrom || row.dueOn > row.windowTo) {
    return `Choose a date between ${formatDateOnly(row.windowFrom)} and ${formatDateOnly(row.windowTo)}.`;
  }
  return null;
}

/**
 * ⭐ `DischargeForm` — HO-SRG-002 §11.6, US-SRG-07. Filled from the server's `discharge-draft`: the post-op
 * instructions (plain text, editable, 4000 with a counter) and the follow-ups with the date window for
 * each (a date outside it is a field error before submit, and the server's message on a `400`). Suture
 * removal can be dropped for *Absorbable sutures*. Submit is disabled until the recovery status is *Ready
 * for discharge* (the server also refuses), then asks for confirmation. Unsaved text is guarded.
 */
export function DischargeForm({
  surgery,
  draft,
  onDischarged,
}: {
  surgery: SurgeryDetail;
  draft: DischargeDraft;
  onDischarged: () => void;
}) {
  const discharge = useDischargeSurgery(surgery.id);
  const [instructions, setInstructions] = useState(draft.postOpInstructions);
  const [rows, setRows] = useState<Row[]>(draft.followUps);
  const [absorbable, setAbsorbable] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [showErrors, setShowErrors] = useState(false);
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const ready = surgery.recoveryStatus === 'ReadyForDischarge';
  const shown = rows.filter((row) => !(absorbable && row.kind === 'SutureRemoval'));
  const hasSutureRow = rows.some((row) => row.kind === 'SutureRemoval');
  const dirty =
    instructions !== draft.postOpInstructions ||
    absorbable ||
    JSON.stringify(rows) !== JSON.stringify(draft.followUps);
  const instructionsError =
    showErrors && !instructions.trim()
      ? 'Write the post-op instructions for the owner.'
      : undefined;

  async function submit() {
    setServerErrors([]);
    try {
      await discharge.mutateAsync({
        postOpInstructions: instructions.trim(),
        followUps: shown.map((row) => ({ kind: row.kind, dueOn: row.dueOn })),
      });
      toast({ title: `${surgery.catName} discharged` });
      onDischarged();
    } catch (error) {
      setServerErrors(
        error instanceof ApiValidationError
          ? surgeryViolations(error).map((v) => v.message)
          : [describeSurgeryError(error)],
      );
    } finally {
      setConfirming(false);
    }
  }

  return (
    <form
      noValidate
      aria-label="Discharge"
      className="flex max-w-3xl flex-col gap-5"
      onSubmit={(event) => {
        event.preventDefault();
        setShowErrors(true);
        if (!instructions.trim() || shown.some((row) => followUpDateError(row))) return;
        setConfirming(true);
      }}
    >
      <UnsavedChangesGuard when={dirty && !discharge.isSuccess} />
      {!ready && (
        <Banner tone="warning" role="status">
          {surgery.catName} is not ready for discharge yet. Set the recovery status to{' '}
          <strong>Ready for discharge</strong> first.
        </Banner>
      )}
      {serverErrors.length > 0 && (
        <Banner tone="danger">
          <ul className="list-disc pl-5">
            {serverErrors.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </Banner>
      )}
      <TextAreaBox
        id="postop-instructions"
        label="Post-op instructions for the owner"
        value={instructions}
        onChange={setInstructions}
        max={SURGERY_LIMITS.postOpInstructionsMax}
        rows={8}
        error={instructionsError}
        hint="Filled in from the procedure’s protocol. Change it if this cat needs something different."
      />

      <fieldset className="flex flex-col gap-3">
        <legend className="text-sm font-semibold text-ink">Follow-ups</legend>
        {shown.length === 0 && <p className={hintClass}>No follow-ups will be scheduled.</p>}
        {shown.map((row) => {
          const id = `followup-${row.kind}`;
          const error = showErrors ? followUpDateError(row) : null;
          return (
            <div key={row.kind} className="flex flex-col gap-1.5">
              <label htmlFor={id} className={labelClass}>
                {followUpLabel(row.kind)} date
              </label>
              <input
                id={id}
                type="date"
                className={inputClass}
                min={row.windowFrom}
                max={row.windowTo}
                value={row.dueOn}
                aria-invalid={error ? true : undefined}
                aria-describedby={`${id}-hint`}
                onChange={(event) =>
                  setRows((all) =>
                    all.map((r) => (r.kind === row.kind ? { ...r, dueOn: event.target.value } : r)),
                  )
                }
              />
              <p id={`${id}-hint`} className={hintClass}>
                Between {formatDateOnly(row.windowFrom)} and {formatDateOnly(row.windowTo)}.
              </p>
              {error && (
                <p role="alert" className={errorClass}>
                  {error}
                </p>
              )}
            </div>
          );
        })}
        {hasSutureRow && (
          <label className="flex items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              className="h-4 w-4"
              checked={absorbable}
              onChange={(event) => setAbsorbable(event.target.checked)}
            />
            Absorbable sutures — no suture removal visit
          </label>
        )}
      </fieldset>

      <div>
        <Button type="submit" disabled={!ready || discharge.isPending}>
          Discharge {surgery.catName}
        </Button>
      </div>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={`Discharge ${surgery.catName}?`}
        consequence={`${surgery.customerName} is sent the post-op instructions, and ${shown.length} follow-up ${
          shown.length === 1 ? 'reminder is' : 'reminders are'
        } scheduled. ${surgery.catName} is recorded as discharged.`}
        confirmLabel="Discharge"
        pendingLabel="Discharging…"
        onConfirm={submit}
      />
    </form>
  );
}
