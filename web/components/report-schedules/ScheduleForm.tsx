'use client';

import { useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { ReportParameterControl } from '@/components/reports/ReportParameterControl';
import { Button } from '@/components/ui/button';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { applyValidationErrors, describeReportError, REPORT_CODE_MESSAGES } from '@/lib/api/errors';
import type { ReportCatalogueEntry } from '@/lib/api/reports';
import {
  DAYS_OF_WEEK,
  REPORT_FREQUENCIES,
  type ReportScheduleRecipient,
} from '@/lib/api/report-schedules';
import { compatiblePeriods, periodHint, periodLabel } from '@/lib/format/report';
import { useEligibleRecipients } from '@/lib/hooks/useReportSchedules';
import {
  reportScheduleSchema,
  toPreviewInput,
  type ReportScheduleFormValues,
} from '@/lib/validation/reportSchedule';
import { NextRunPreview } from './NextRunPreview';
import { RecipientPicker } from './RecipientPicker';

const INPUT =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

type FieldName = keyof ReportScheduleFormValues;
/** The server's `propertyName` (Pascal- or camelCase) -> this form's field. */
const FIELD_MAP: Record<string, FieldName> = {
  Name: 'name',
  name: 'name',
  ReportKey: 'reportKey',
  reportKey: 'reportKey',
  BranchId: 'branchId',
  branchId: 'branchId',
  Period: 'period',
  period: 'period',
  Format: 'format',
  format: 'format',
  Frequency: 'frequency',
  frequency: 'frequency',
  DayOfWeek: 'dayOfWeek',
  dayOfWeek: 'dayOfWeek',
  DayOfMonth: 'dayOfMonth',
  dayOfMonth: 'dayOfMonth',
  SendAtLocal: 'sendAtLocal',
  sendAtLocal: 'sendAtLocal',
  RecipientIds: 'recipientIds',
  recipientIds: 'recipientIds',
};
const KNOWN_FIELDS = [...new Set(Object.values(FIELD_MAP))];

export interface ScheduleBranchOption {
  id: string;
  name: string;
}

function Row({
  id,
  label,
  hint,
  error,
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
      </label>
      {children}
      {hint && <p className="m-0 text-xs text-ink-muted">{hint}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className="m-0 text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}

/**
 * `components/report-schedules/ScheduleForm.tsx` — ⭐ HO-RPT-002 §11.5, US-RPT-06, NFR-USA-5. The
 * create / edit form. The report list is the schedulable catalogue; the periods offered follow the
 * report (U-E1); recipients come from the server (U-E2); the timing preview is the server's
 * (ADR-FE-136). Values survive a server error (the form is never reset) and a server `400` lands on
 * the field its `propertyName` names (U-E5); `UnsavedChangesGuard` covers navigation while dirty.
 */
export function ScheduleForm({
  reports,
  initial,
  keptRecipients = [],
  branches,
  zoneFor,
  onSubmit,
  isPending,
  submitLabel,
  onReload,
}: {
  reports: ReportCatalogueEntry[];
  initial: ReportScheduleFormValues;
  keptRecipients?: ReportScheduleRecipient[];
  branches: ScheduleBranchOption[];
  zoneFor: (branchId: string) => string;
  onSubmit: (values: ReportScheduleFormValues) => Promise<unknown>;
  isPending: boolean;
  submitLabel: string;
  onReload?: () => void;
}) {
  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    watch,
    formState: { errors, isDirty, isSubmitSuccessful },
  } = useForm<ReportScheduleFormValues>({
    resolver: zodResolver(reportScheduleSchema),
    defaultValues: initial,
  });
  const [conflict, setConflict] = useState(false);

  const values = watch();
  const report = reports.find((entry) => entry.key === values.reportKey);
  const periods = useMemo(() => (report ? compatiblePeriods(report) : []), [report]);
  const recipients = useEligibleRecipients(values.reportKey, values.branchId || null);
  const canAll = report?.canUseAllBranches ?? false;

  function chooseReport(key: string) {
    const next = reports.find((entry) => entry.key === key);
    setValue('reportKey', key, { shouldDirty: true });
    setValue('parameters', {}, { shouldDirty: true });
    setValue('recipientIds', [], { shouldDirty: true });
    // '' means "all branches", which only some reports allow: never leave it selected invisibly.
    if (next && !next.canUseAllBranches && values.branchId === '') {
      setValue('branchId', branches[0]?.id ?? '', { shouldDirty: true });
    }
    if (next && !compatiblePeriods(next).includes(values.period)) {
      setValue('period', next.defaultPeriod, { shouldDirty: true });
    }
  }

  async function submit(formValues: ReportScheduleFormValues) {
    setConflict(false);
    try {
      await onSubmit(formValues);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: FIELD_MAP,
          knownFields: KNOWN_FIELDS,
          codeMessages: REPORT_CODE_MESSAGES,
        });
      } else {
        if (error instanceof ApiConflictError) setConflict(true);
        setError('root', { type: 'server', message: describeReportError(error) });
      }
    }
  }

  const fieldError = (name: FieldName) => errors[name]?.message as string | undefined;
  const recipientError = errors.recipientIds?.message as string | undefined;

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />
      <form
        aria-label="Schedule"
        noValidate
        onSubmit={handleSubmit(submit)}
        className="flex min-w-0 flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
      >
        <Row
          id="sched-name"
          label="Name"
          hint="Up to 100 characters. Used as the email subject."
          error={fieldError('name')}
        >
          <input id="sched-name" type="text" className={INPUT} {...register('name')} />
        </Row>

        <Row id="sched-report" label="Report" error={fieldError('reportKey')}>
          <select
            id="sched-report"
            className={INPUT}
            value={values.reportKey}
            onChange={(event) => chooseReport(event.target.value)}
          >
            <option value="">Choose a report</option>
            {reports.map((entry) => (
              <option key={entry.key} value={entry.key}>
                {entry.title}
              </option>
            ))}
          </select>
        </Row>

        <Row id="sched-branch" label="Branch" error={fieldError('branchId')}>
          <select id="sched-branch" className={INPUT} {...register('branchId')}>
            {canAll && <option value="">All branches</option>}
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
          </select>
        </Row>

        <Row
          id="sched-period"
          label="Period each run covers"
          hint={periodHint(values.period)}
          error={fieldError('period')}
        >
          <select id="sched-period" className={INPUT} {...register('period')}>
            <option value="">Choose a period</option>
            {periods.map((period) => (
              <option key={period} value={period}>
                {periodLabel(period)}
              </option>
            ))}
          </select>
        </Row>

        {report?.parameters.map((parameter) => (
          <ReportParameterControl
            key={parameter.name}
            idPrefix="sched-param"
            parameter={parameter}
            value={values.parameters[parameter.name] ?? ''}
            onChange={(value) =>
              setValue(
                'parameters',
                { ...values.parameters, [parameter.name]: value },
                { shouldDirty: true },
              )
            }
          />
        ))}

        <fieldset className="m-0 border-0 p-0">
          <legend className="mb-1 text-sm font-semibold text-ink">Format</legend>
          <div className="flex gap-4 text-sm">
            {(['Csv', 'Pdf'] as const).map((format) => (
              <label key={format} className="flex items-center gap-1.5">
                <input type="radio" value={format} {...register('format')} />
                {format.toUpperCase()}
              </label>
            ))}
          </div>
        </fieldset>

        <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-sm font-semibold text-ink">Frequency</legend>
          <div className="flex flex-wrap gap-4 text-sm">
            {REPORT_FREQUENCIES.map((frequency) => (
              <label key={frequency} className="flex items-center gap-1.5">
                <input type="radio" value={frequency} {...register('frequency')} />
                {frequency}
              </label>
            ))}
          </div>
          {values.frequency === 'Weekly' && (
            <Row id="sched-dow" label="Day of the week" error={fieldError('dayOfWeek')}>
              <select id="sched-dow" className={INPUT} {...register('dayOfWeek')}>
                <option value="">Choose a day</option>
                {DAYS_OF_WEEK.map((day) => (
                  <option key={day} value={day}>
                    {day}
                  </option>
                ))}
              </select>
            </Row>
          )}
          {values.frequency === 'Monthly' && (
            <div className="flex flex-wrap items-end gap-4 text-sm">
              <label className="flex items-center gap-1.5">
                <input type="radio" value="day" {...register('monthlyMode')} />
                On day
              </label>
              <Row id="sched-dom" label="Day of the month" error={fieldError('dayOfMonth')}>
                <input
                  id="sched-dom"
                  type="number"
                  min={1}
                  max={28}
                  disabled={values.monthlyMode === 'last'}
                  className={`${INPUT} max-w-24`}
                  {...register('dayOfMonth')}
                />
              </Row>
              <label className="flex items-center gap-1.5">
                <input type="radio" value="last" {...register('monthlyMode')} />
                On the last day of the month
              </label>
            </div>
          )}
        </fieldset>

        <Row
          id="sched-time"
          label={`Send time (${zoneFor(values.branchId)})`}
          hint="In 5-minute steps."
          error={fieldError('sendAtLocal')}
        >
          <input
            id="sched-time"
            type="time"
            step={300}
            className={`${INPUT} max-w-40`}
            {...register('sendAtLocal')}
          />
        </Row>

        {values.reportKey && recipients.isError ? (
          <p role="alert" className="m-0 text-sm font-semibold text-danger-ink">
            {describeReportError(recipients.error)}
          </p>
        ) : (
          <Controller
            control={control}
            name="recipientIds"
            render={({ field }) => (
              <RecipientPicker
                eligible={recipients.data ?? []}
                kept={keptRecipients}
                selected={field.value}
                onChange={field.onChange}
                error={recipientError}
              />
            )}
          />
        )}

        {errors.root?.message && (
          <div
            role="alert"
            className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
          >
            {errors.root.message}
            {conflict && onReload && (
              <Button variant="secondary" size="sm" className="ml-2" onClick={onReload}>
                Reload the latest
              </Button>
            )}
          </div>
        )}

        <div className="flex justify-end gap-2 border-t border-border pt-3">
          <Button type="submit" disabled={isPending || isSubmitSuccessful} aria-busy={isPending}>
            {isPending ? 'Saving…' : submitLabel}
          </Button>
        </div>
      </form>

      <div className="min-w-0">
        <NextRunPreview input={toPreviewInput(values)} timeZoneId={zoneFor(values.branchId)} />
      </div>
    </div>
  );
}
