'use client';

import { AlertTriangle, XCircle } from 'lucide-react';
import { TextAreaField } from '@/components/inventory/fields';
import type { SafetyAssessment } from '@/lib/api/prescriptions';
import {
  isAllergy,
  isDoseWarning,
  warningKey,
  type AckProblems,
  type Acknowledgement,
} from './composerModel';

/**
 * ⭐ `AcknowledgementBlock` — HO-PHM-002 §11.2, ADR-FE-060. Appears ONLY when the server says a warning
 * `requiresAcknowledgement`: one checkbox per such warning (*"I have reviewed: Dose above range"*).
 * An allergy conflict additionally needs a **reason** (500); a dose warning takes an optional note.
 * The ticks belong to the fingerprint the vet was shown: when the fingerprint changes the composer
 * clears them and `changed` says so, in words (*"Warnings changed — review again"*).
 */
export function AcknowledgementBlock({
  assessment,
  ack,
  problems,
  changed,
  onTick,
  onReason,
  onNote,
}: {
  assessment: SafetyAssessment;
  ack: Acknowledgement;
  /** Shown after a submit attempt only. */
  problems: AckProblems | null;
  changed: boolean;
  onTick: (key: string, ticked: boolean) => void;
  onReason: (value: string) => void;
  onNote: (value: string) => void;
}) {
  const needing = assessment.warnings
    .map((warning, index) => ({ warning, index, key: warningKey(warning, index) }))
    .filter((entry) => entry.warning.requiresAcknowledgement);
  const allergy = assessment.warnings.some(isAllergy);
  const dose = assessment.warnings.some(isDoseWarning);
  if (needing.length === 0 && !changed) return null;

  return (
    <fieldset className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-warning-ink bg-warning-bg p-4">
      <legend className="flex items-center gap-2 px-1 text-sm font-bold text-warning-ink">
        <AlertTriangle size={16} aria-hidden="true" />
        {needing.length > 0
          ? `Acknowledge before issuing — ${needing.length} ${needing.length === 1 ? 'warning needs' : 'warnings need'} your review`
          : 'Warnings'}
      </legend>
      {changed && (
        <p role="status" className="text-sm font-semibold text-warning-ink">
          Warnings changed — review again.
        </p>
      )}
      {needing.map(({ warning, index, key }) => {
        const error = problems?.ticks[key];
        return (
          <div key={key} className="flex flex-col gap-1">
            <label className="flex items-start gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={Boolean(ack.ticks[key])}
                aria-invalid={!!error}
                aria-describedby={error ? `ack-w-${index}-error` : undefined}
                onChange={(event) => onTick(key, event.target.checked)}
                className="mt-0.5 h-4 w-4 flex-none accent-[var(--accent)]"
              />
              <span>
                I have reviewed: <b>{warning.subject}</b>
              </span>
            </label>
            {error && (
              <p
                id={`ack-w-${index}-error`}
                role="alert"
                className="ml-6 flex items-center gap-1 text-xs font-semibold text-danger-ink"
              >
                <XCircle size={12} aria-hidden="true" />
                {error}
              </p>
            )}
          </div>
        );
      })}
      {allergy && (
        <TextAreaField
          id="ack-allergy-reason"
          label="Why are you prescribing despite the allergy?"
          required
          rows={3}
          maxLength={500}
          value={ack.allergyReason}
          hint="Recorded with the prescription. 500 characters at most."
          error={problems?.allergyReason}
          onChange={(event) => onReason(event.target.value)}
        />
      )}
      {dose && (
        <TextAreaField
          id="ack-dose-note"
          label="Note on the dose (optional)"
          rows={2}
          maxLength={500}
          value={ack.doseNote}
          onChange={(event) => onNote(event.target.value)}
        />
      )}
    </fieldset>
  );
}
