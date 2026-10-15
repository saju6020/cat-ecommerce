'use client';

import { useState } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { WriteButton } from '@/components/common/WriteButton';
import { Button } from '@/components/ui/button';
import { useRecordRestoreTest } from '@/lib/hooks/useOperations';
import { ApiValidationError } from '@/lib/api/client';
import { applyValidationErrors, describeOperationsError, OPERATIONS_CODE_MESSAGES } from '@/lib/api/errors';
import type { RecordRestoreTestResult, RestoreTestSummary } from '@/lib/api/operations';
import { hasUtcOffset, parseDrillOutput } from '@/lib/operations/drill';
import { formatDateOnly } from '@/lib/format/time';
import { OutcomeBadge } from './RestoreTestsTable';

const instant = z
  .string()
  .trim()
  .refine(
    (v) => v !== '' && !Number.isNaN(Date.parse(v)) && hasUtcOffset(v),
    'Enter a date and time with its offset, like 2026-10-10T09:00:00+06:00 or ...Z. Without one the moment is a guess.',
  );
const schema = z.object({
  performedOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter the date of the drill.'),
  environment: z.string().trim().min(1, 'Enter the environment.'),
  backupSetLabel: z.string().trim().min(1, 'Enter the backup set label.'),
  simulatedFailureAt: instant,
  recoveredTo: instant,
  restoreStartedAt: instant,
  restoreCompletedAt: instant,
  checks: z
    .array(z.object({ name: z.string().trim().min(1, 'Name the check.'), passed: z.boolean(), detail: z.string().nullable() }))
    .min(1, 'Add at least one check from the drill output.'),
  notes: z.string().max(2000, 'Keep notes under 2000 characters.').optional(),
  supersedesId: z.string().optional(),
});
type FormValues = z.infer<typeof schema>;

const EMPTY: FormValues = {
  performedOn: '', environment: '', backupSetLabel: '', simulatedFailureAt: '', recoveredTo: '',
  restoreStartedAt: '', restoreCompletedAt: '', checks: [], notes: '', supersedesId: '',
};

const KNOWN_FIELDS = [
  'performedOn', 'environment', 'backupSetLabel', 'simulatedFailureAt', 'recoveredTo',
  'restoreStartedAt', 'restoreCompletedAt', 'checks', 'notes', 'supersedesId',
] as const;
const FIELD_MAP = {
  PerformedOn: 'performedOn', Environment: 'environment', BackupSetLabel: 'backupSetLabel',
  SimulatedFailureAt: 'simulatedFailureAt', RecoveredTo: 'recoveredTo', RestoreStartedAt: 'restoreStartedAt',
  RestoreCompletedAt: 'restoreCompletedAt', Checks: 'checks', Notes: 'notes', SupersedesId: 'supersedesId',
} as const;

/** The API stores instants as UTC and answers a non-zero offset with a 500 (HO-XCT-002 section 1
 *  Amended 2026-10-14), so every instant leaves this form as the same moment written in UTC. */
function toUtc(value: string): string {
  return new Date(value.trim()).toISOString();
}

const PASTE_FIRST_MESSAGE =
  'Paste the drill output first. It is kept with the record as the evidence of the test.';

const inputClass = 'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

/**
 * `components/operations/RecordRestoreTestForm.tsx` — HO-XCT-002 §11.5, U-B8/U-B9, US-XCT-05 AC2.
 * "Paste the drill JSON" fills the fields from `restore-drill.sh`'s output; they stay editable. A
 * parse error keeps what was pasted. There is NO RPO, RTO or outcome input: the server computes
 * them, and the result panel shows what it decided. The raw pasted text is stored as the drill
 * output and shown later as text only.
 */
export function RecordRestoreTestForm({ previousTests }: { previousTests: RestoreTestSummary[] }) {
  const [pasted, setPasted] = useState('');
  const [parseError, setParseError] = useState<string | null>(null);
  const [result, setResult] = useState<RecordRestoreTestResult | null>(null);
  const record = useRecordRestoreTest();
  const { register, control, handleSubmit, reset, setError, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: EMPTY,
  });
  const { fields } = useFieldArray({ control, name: 'checks' });

  function fillFromPaste() {
    const parsed = parseDrillOutput(pasted);
    if (!parsed.ok) {
      setParseError(parsed.message);
      return;
    }
    setParseError(null);
    reset({ ...EMPTY, ...parsed.fields });
  }

  async function onSubmit(values: FormValues) {
    setResult(null);
    // The drill output is the evidence behind the record: exactly what was pasted, or nothing to
    // send. It is never rebuilt from the form's fields (HO-XCT-001 section 12.4, ARCHITECTURE gate).
    try {
      setResult(
        await record.mutateAsync({
          performedOn: values.performedOn,
          environment: values.environment.trim(),
          backupSetLabel: values.backupSetLabel.trim(),
          simulatedFailureAt: toUtc(values.simulatedFailureAt),
          recoveredTo: toUtc(values.recoveredTo),
          restoreStartedAt: toUtc(values.restoreStartedAt),
          restoreCompletedAt: toUtc(values.restoreCompletedAt),
          checks: values.checks,
          drillOutput: pasted,
          notes: values.notes?.trim() || undefined,
          supersedesId: values.supersedesId || undefined,
        }),
      );
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: FIELD_MAP,
          knownFields: KNOWN_FIELDS,
          codeMessages: OPERATIONS_CODE_MESSAGES,
        });
      } else {
        setError('root', { message: describeOperationsError(error) });
      }
    }
  }

  return (
    <section aria-labelledby="record-test-title" className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h2 id="record-test-title" className="text-[15px] font-semibold text-ink">Record a restore test</h2>
      <div className="flex flex-col gap-2">
        <label htmlFor="drill-paste" className="text-sm font-semibold text-ink">Paste the drill JSON</label>
        <textarea id="drill-paste" rows={6} value={pasted} onChange={(e) => setPasted(e.target.value)}
          aria-describedby={parseError ? 'drill-paste-error' : undefined} aria-invalid={Boolean(parseError)}
          className={`${inputClass} font-mono text-xs`} />
        {parseError && <p id="drill-paste-error" role="alert" className="text-sm text-danger-ink">{parseError}</p>}
        <div><Button variant="secondary" size="sm" onClick={fillFromPaste} disabled={pasted.trim() === ''}>Fill the fields from this</Button></div>
      </div>

      <form
        onSubmit={(event) => {
          // The paste is checked first, so its message shows even while other fields are still empty.
          if (pasted.trim() === '') {
            event.preventDefault();
            setError('root', { message: PASTE_FIRST_MESSAGE });
            return;
          }
          void handleSubmit(onSubmit)(event);
        }}
        noValidate
        className="flex flex-col gap-3"
      >
        <div className="grid gap-3 sm:grid-cols-2">
          {([
            ['performedOn', 'Date of the drill', 'date'],
            ['environment', 'Environment', 'text'],
            ['backupSetLabel', 'Backup set label', 'text'],
            ['simulatedFailureAt', 'Failure simulated at', 'text'],
            ['recoveredTo', 'Data recovered up to', 'text'],
            ['restoreStartedAt', 'Restore started at', 'text'],
            ['restoreCompletedAt', 'Restore completed at', 'text'],
          ] as const).map(([name, label, type]) => (
            <div key={name} className="flex flex-col gap-1">
              <label htmlFor={`rt-${name}`} className="text-sm font-semibold text-ink">{label}</label>
              <input id={`rt-${name}`} type={type} aria-invalid={Boolean(errors[name])}
                aria-describedby={errors[name] ? `rt-${name}-error` : undefined} className={inputClass} {...register(name)} />
              {errors[name] && <p id={`rt-${name}-error`} role="alert" className="text-sm text-danger-ink">{errors[name]?.message}</p>}
            </div>
          ))}
        </div>

        <fieldset className="flex flex-col gap-2">
          <legend className="text-sm font-semibold text-ink">Checks from the drill</legend>
          {fields.length === 0 && <p className="text-sm text-ink-muted">Paste the drill output above to fill these in.</p>}
          {fields.map((field, index) => (
            <div key={field.id} className="grid gap-2 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
              <input aria-label={`Check ${index + 1} name`} className={inputClass} {...register(`checks.${index}.name`)} />
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" {...register(`checks.${index}.passed`)} /> Passed
              </label>
              <input aria-label={`Check ${index + 1} detail`} className={inputClass} {...register(`checks.${index}.detail`, { setValueAs: (v) => (v === '' ? null : v) })} />
            </div>
          ))}
          {errors.checks?.message && <p role="alert" className="text-sm text-danger-ink">{errors.checks.message}</p>}
        </fieldset>

        <div className="flex flex-col gap-1">
          <label htmlFor="rt-notes" className="text-sm font-semibold text-ink">Notes (optional)</label>
          <textarea id="rt-notes" rows={2} className={inputClass} {...register('notes')} />
        </div>
        {previousTests.length > 0 && (
          <div className="flex flex-col gap-1">
            <label htmlFor="rt-supersedes" className="text-sm font-semibold text-ink">This test replaces (optional)</label>
            <select id="rt-supersedes" className={inputClass} {...register('supersedesId')}>
              <option value="">Nothing — a new test</option>
              {previousTests.map((t) => (
                <option key={t.id} value={t.id}>{formatDateOnly(t.performedOn)} · {t.backupSetLabel}</option>
              ))}
            </select>
          </div>
        )}
        <p className="text-sm text-ink-muted">The data lost, the time to restore and whether the test passed are worked out by PurrCare from these times and checks.</p>
        {errors.root && <p role="alert" className="text-sm text-danger-ink">{errors.root.message}</p>}
        <div><WriteButton type="submit">Record restore test</WriteButton></div>
      </form>

      {result && (
        <div role="status" className="rounded-[var(--radius-md)] border border-border bg-surface-sunken p-3 text-sm">
          <p className="flex flex-wrap items-center gap-2 font-semibold text-ink">Recorded. PurrCare worked out: <OutcomeBadge outcome={result.outcome} /></p>
          <p className="text-ink-muted">Data lost {result.achievedRpoMinutes} minutes · restored in {result.achievedRtoMinutes} minutes.</p>
        </div>
      )}
    </section>
  );
}
