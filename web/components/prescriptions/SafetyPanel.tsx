'use client';

import * as React from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Info,
  Loader2,
  OctagonX,
  Scale,
  ShieldAlert,
  XCircle,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ClinicalText, Quantity } from '@/components/common/Quantity';
import type { SafetyAssessment, SafetyWarning } from '@/lib/api/prescriptions';
import { formatDoseRange } from '@/lib/format/units';
import { formatDay } from '@/lib/format/pharmacy';
import { cn } from '@/lib/utils';

/**
 * ⭐ `SafetyPanel` — HO-PHM-002 §11.2, ADR-FE-060 / ADR-FE-068, UI-6. Renders the SERVER's assessment
 * and nothing else: the weight, the dose check with the expected range in mg/kg AND mg, the warnings
 * (an allergy conflict first, labelled **Blocking**; interactions by severity; dose), and the
 * information the server sent. It never computes a dose, matches an allergy or judges a range.
 *
 * Severity is a word plus an icon, never colour alone. While the next check runs, the last result
 * stays in place under a *Checking…* line (nothing flickers away). Screen readers get one polite
 * announcement per CHANGE ("1 new warning: dose above range") from a separate live region — not a
 * re-read of the whole panel on every re-check.
 */
const ALLERGY_LIMITATION =
  'Allergy checks compare recorded allergies with this medicine’s ingredients and classes. A misspelt allergy may not be matched.';

function severityBadge(warning: SafetyWarning) {
  if (warning.blocking)
    return (
      <Badge variant="danger" icon={<OctagonX size={12} aria-hidden="true" />}>
        Blocking
      </Badge>
    );
  const severity = warning.severity ?? (warning.kind === 'DoseNotCheckable' ? 'Note' : 'Warning');
  if (severity === 'Major' || severity === 'Contraindicated')
    return (
      <Badge variant="danger" icon={<XCircle size={12} aria-hidden="true" />}>
        {severity}
      </Badge>
    );
  if (severity === 'Minor' || severity === 'Note')
    return (
      <Badge variant="neutral" icon={<Info size={12} aria-hidden="true" />}>
        {severity}
      </Badge>
    );
  return (
    <Badge variant="warning" icon={<AlertTriangle size={12} aria-hidden="true" />}>
      {severity}
    </Badge>
  );
}

/** One polite sentence describing what CHANGED between two assessments; empty when nothing did. */
export function describeChange(
  previous: SafetyAssessment | undefined,
  next: SafetyAssessment | undefined,
): string {
  if (!previous || !next || previous.fingerprint === next.fingerprint) return '';
  const before = new Set(previous.warnings.map((w) => `${w.kind}:${w.subject}`));
  const after = new Set(next.warnings.map((w) => `${w.kind}:${w.subject}`));
  const added = next.warnings.filter((w) => !before.has(`${w.kind}:${w.subject}`));
  const removed = previous.warnings.filter((w) => !after.has(`${w.kind}:${w.subject}`));
  const parts: string[] = [];
  if (added.length > 0)
    parts.push(
      `${added.length} new ${added.length === 1 ? 'warning' : 'warnings'}: ${added.map((w) => w.subject.toLowerCase()).join(', ')}`,
    );
  if (removed.length > 0)
    parts.push(`${removed.length} ${removed.length === 1 ? 'warning' : 'warnings'} cleared`);
  return parts.join('. ');
}

export function SafetyPanel({
  assessment,
  isChecking,
  failed,
  hasInput,
  announcement,
  onRecordWeight,
  weightRef,
}: {
  assessment: SafetyAssessment | undefined;
  isChecking: boolean;
  failed: boolean;
  /** The form has a medicine, a dose with its unit, a route and a frequency. */
  hasInput: boolean;
  announcement: string;
  onRecordWeight: () => void;
  weightRef?: React.Ref<HTMLHeadingElement>;
}) {
  return (
    <aside
      aria-label="Safety check"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <p role="status" aria-live="polite" className="sr-only">
        {announcement}
      </p>
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-display text-base font-semibold text-ink">Safety check</h2>
        {isChecking && (
          <span className="inline-flex items-center gap-1 text-xs text-ink-muted">
            <Loader2
              size={12}
              className="animate-spin motion-reduce:animate-none"
              aria-hidden="true"
            />
            Checking…
          </span>
        )}
      </div>

      {failed && (
        <p role="alert" className="flex gap-2 text-sm font-semibold text-danger-ink">
          <XCircle size={16} className="mt-0.5 flex-none" aria-hidden="true" />
          The safety check could not run. You cannot issue until it does.
        </p>
      )}

      {!assessment && !failed && (
        <p className="text-sm text-ink-muted">
          {hasInput
            ? 'Checking…'
            : 'Choose a medicine and enter the dose, route and how often. The server checks it against this cat’s weight, allergies and current medicines.'}
        </p>
      )}

      {assessment && (
        <>
          <section aria-labelledby="sp-weight" className="flex flex-col gap-1.5">
            <h3
              id="sp-weight"
              ref={weightRef}
              tabIndex={-1}
              className="text-xs font-bold uppercase tracking-wide text-ink-muted"
            >
              Weight
            </h3>
            {assessment.weight.requirement === 'Satisfied' && assessment.weight.latest ? (
              <p className="flex items-center gap-2 text-sm text-accent-ink">
                <CheckCircle2 size={16} aria-hidden="true" />
                <span>
                  Latest weight <Quantity as="b" value={assessment.weight.latest.weight} />,{' '}
                  {formatDay(assessment.weight.latest.recordedOn)}
                </span>
              </p>
            ) : (
              <div className="flex flex-col gap-2">
                <p className="flex items-start gap-2 text-sm font-semibold text-warning-ink">
                  <Scale size={16} className="mt-0.5 flex-none" aria-hidden="true" />
                  <span>
                    {assessment.weight.latest ? (
                      <>
                        The latest weight is out of date:{' '}
                        <Quantity value={assessment.weight.latest.weight} /> on{' '}
                        {formatDay(assessment.weight.latest.recordedOn)}. Record a new weight before
                        prescribing.
                      </>
                    ) : (
                      'No weight has been recorded. Record one before prescribing.'
                    )}
                  </span>
                </p>
                <div>
                  <Button variant="secondary" size="sm" onClick={onRecordWeight}>
                    Record weight
                  </Button>
                </div>
              </div>
            )}
          </section>

          <section aria-labelledby="sp-dose" className="flex flex-col gap-1.5">
            <h3 id="sp-dose" className="text-xs font-bold uppercase tracking-wide text-ink-muted">
              Dose check
            </h3>
            <DoseCheck assessment={assessment} />
          </section>

          <section aria-labelledby="sp-warnings" className="flex flex-col gap-2">
            <h3
              id="sp-warnings"
              className="text-xs font-bold uppercase tracking-wide text-ink-muted"
            >
              Warnings ({assessment.warnings.length})
            </h3>
            {assessment.warnings.length === 0 ? (
              <p className="flex items-center gap-2 text-sm text-ink-muted">
                <CheckCircle2 size={16} aria-hidden="true" />
                No warnings.
              </p>
            ) : (
              <ul className="flex list-none flex-col gap-2 p-0">
                {assessment.warnings.map((warning, index) => (
                  <li
                    key={`${warning.kind}-${warning.subject}-${index}`}
                    className={cn(
                      'rounded-[var(--radius-sm)] border p-3 text-sm',
                      warning.blocking
                        ? 'border-danger-ink bg-danger-bg'
                        : 'border-border bg-surface-sunken',
                    )}
                  >
                    <p className="mb-1 flex flex-wrap items-center gap-2">
                      {severityBadge(warning)}
                      <b className="text-ink">{warning.subject}</b>
                    </p>
                    <p className="whitespace-pre-wrap break-words text-ink">{warning.detail}</p>
                    <p className="mt-1 text-xs font-semibold text-ink-muted">
                      {warning.requiresAcknowledgement
                        ? 'Needs acknowledgement'
                        : 'No acknowledgement needed'}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section aria-labelledby="sp-info" className="flex flex-col gap-1.5">
            <h3 id="sp-info" className="text-xs font-bold uppercase tracking-wide text-ink-muted">
              Information
            </h3>
            <ul className="m-0 flex list-none flex-col gap-1 p-0 text-sm text-ink">
              {assessment.information.map((line) => (
                <li key={line} className="flex gap-2 whitespace-pre-wrap break-words">
                  <Info size={14} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
                  {line}
                </li>
              ))}
              {!assessment.hasRecordedAllergies &&
                !assessment.information.some((l) => /no allergies/i.test(l)) && (
                  <li className="flex gap-2">
                    <Info
                      size={14}
                      className="mt-0.5 flex-none text-ink-muted"
                      aria-hidden="true"
                    />
                    No allergies recorded.
                  </li>
                )}
            </ul>
          </section>
        </>
      )}

      <p className="flex gap-2 border-t border-border pt-3 text-xs text-ink-muted">
        <ShieldAlert size={14} className="mt-0.5 flex-none" aria-hidden="true" />
        {ALLERGY_LIMITATION}
      </p>
    </aside>
  );
}

function DoseCheck({ assessment }: { assessment: SafetyAssessment }) {
  const { doseCheck, weight } = assessment;
  const range = doseCheck.expectedRange;
  const rangeText = range ? formatDoseRange(range, weight.latest?.weight ?? null) : null;
  if (doseCheck.outcome === 'NotCheckable') {
    return (
      <p className="flex gap-2 text-sm text-ink-muted">
        <Info size={16} className="mt-0.5 flex-none" aria-hidden="true" />
        This dose could not be checked against a mg/kg range.
      </p>
    );
  }
  const within = doseCheck.outcome === 'WithinRange';
  const label =
    doseCheck.outcome === 'AboveRange'
      ? 'Above range'
      : doseCheck.outcome === 'BelowRange'
        ? 'Below range'
        : 'Within range';
  return (
    <div className="flex flex-col gap-1 text-sm">
      <p className={cn('flex items-start gap-2', within ? 'text-accent-ink' : 'text-warning-ink')}>
        {within ? (
          <CheckCircle2 size={16} className="mt-0.5 flex-none" aria-hidden="true" />
        ) : (
          <AlertTriangle size={16} className="mt-0.5 flex-none" aria-hidden="true" />
        )}
        <span>
          <b>{label}</b>
          {doseCheck.doseMgPerKg && (
            <>
              {' — '}
              <ClinicalText>{doseCheck.doseMgPerKg}&nbsp;mg/kg</ClinicalText>
              {doseCheck.doseMg && (
                <>
                  {' ('}
                  <Quantity value={doseCheck.doseMg} />
                  {')'}
                </>
              )}
            </>
          )}
        </span>
      </p>
      {rangeText && (
        <p className="ml-6 text-xs text-ink-muted">
          Expected <ClinicalText>{rangeText}</ClinicalText>
        </p>
      )}
    </div>
  );
}
