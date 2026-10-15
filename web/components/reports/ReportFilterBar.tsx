'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ALL_BRANCHES_TOKEN, GROUP_BY_OPTIONS, type ReportCatalogueEntry } from '@/lib/api/reports';
import type { ReportFilterValues } from '@/lib/hooks/useReportFilters';
import { periodLabel } from '@/lib/format/report';
import { timeFieldLabel } from '@/lib/format/time';
import { ReportParameterControl } from './ReportParameterControl';

const INPUT =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

export interface BranchOption {
  id: string;
  name: string;
}

/**
 * `components/reports/ReportFilterBar.tsx` — ⭐ HO-RPT-002 §11.3, ADR-FE-129/131. Rendered ENTIRELY
 * from the catalogue definition: the date range (the server's `defaultRange` is the one preset the
 * page can offer — it has no endpoint to resolve "Yesterday" or "Last week", and doing that date
 * arithmetic here would break ADR-FE-131; HO-RPT-002 §1 Amended), two `yyyy-MM-dd` text fields
 * labelled with the zone, the branch (`All branches` only when `canUseAllBranches`), "Group by" for
 * time-bucketed reports, and one control per parameter. **Apply** hands the draft to the caller,
 * which writes the URL; **Reset** returns to the server's defaults. The range limit is shown before
 * submitting; the server enforces it, so a refusal arrives as a field error (`errors`).
 */
export function ReportFilterBar({
  definition,
  applied,
  timeZoneId,
  branches,
  errors,
  onApply,
  onReset,
}: {
  definition: ReportCatalogueEntry;
  applied: ReportFilterValues;
  timeZoneId: string;
  branches: BranchOption[];
  /** Field name (`from`, `to`, `branchId`, `groupBy`, or a parameter name) -> message. */
  errors: Record<string, string>;
  onApply: (values: ReportFilterValues) => void;
  onReset: () => void;
}) {
  const [draft, setDraft] = useState<ReportFilterValues>(applied);
  const isDefaultRange =
    draft.from === definition.defaultRange.from && draft.to === definition.defaultRange.to;

  function update(patch: Partial<ReportFilterValues>) {
    setDraft((current) => ({ ...current, ...patch }));
  }

  return (
    <form
      aria-label="Report filters"
      onSubmit={(event) => {
        event.preventDefault();
        onApply(draft);
      }}
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-1 sm:col-span-2 lg:col-span-4">
          <span id="range-presets" className="text-sm font-semibold text-ink">
            Date range
          </span>
          <div role="group" aria-labelledby="range-presets" className="flex flex-wrap gap-2">
            <button
              type="button"
              aria-pressed={isDefaultRange}
              onClick={() =>
                update({ from: definition.defaultRange.from, to: definition.defaultRange.to })
              }
              className="rounded-full border border-border-strong px-3 py-1 text-sm font-semibold text-ink aria-pressed:bg-accent aria-pressed:text-white"
            >
              Default: {periodLabel(definition.defaultPeriod)}
            </button>
            <span className="self-center text-xs text-ink-muted">
              Or choose your own dates below.
            </span>
          </div>
        </div>

        <FieldShell id="report-from" label={timeFieldLabel('From', timeZoneId)} error={errors.from}>
          <input
            id="report-from"
            type="text"
            inputMode="numeric"
            placeholder="yyyy-MM-dd"
            value={draft.from}
            aria-invalid={errors.from ? true : undefined}
            aria-describedby={errors.from ? 'report-from-error' : undefined}
            onChange={(event) => update({ from: event.target.value })}
            className={INPUT}
          />
        </FieldShell>

        <FieldShell
          id="report-to"
          label={timeFieldLabel('To', timeZoneId)}
          error={errors.to}
          hint={`Up to ${definition.maxRangeDays} days`}
        >
          <input
            id="report-to"
            type="text"
            inputMode="numeric"
            placeholder="yyyy-MM-dd"
            value={draft.to}
            aria-invalid={errors.to ? true : undefined}
            aria-describedby={errors.to ? 'report-to-error' : undefined}
            onChange={(event) => update({ to: event.target.value })}
            className={INPUT}
          />
        </FieldShell>

        <FieldShell id="report-branch" label="Branch" error={errors.branchId}>
          <select
            id="report-branch"
            value={draft.branchId}
            aria-invalid={errors.branchId ? true : undefined}
            aria-describedby={errors.branchId ? 'report-branch-error' : undefined}
            onChange={(event) => update({ branchId: event.target.value })}
            className={INPUT}
          >
            <option value="">Active branch</option>
            {branches.map((branch) => (
              <option key={branch.id} value={branch.id}>
                {branch.name}
              </option>
            ))}
            {definition.canUseAllBranches && (
              <option value={ALL_BRANCHES_TOKEN}>All branches</option>
            )}
          </select>
        </FieldShell>

        {definition.timeBucketed && (
          <FieldShell id="report-group-by" label="Group by" error={errors.groupBy}>
            <select
              id="report-group-by"
              value={draft.groupBy}
              aria-invalid={errors.groupBy ? true : undefined}
              aria-describedby={errors.groupBy ? 'report-group-by-error' : undefined}
              onChange={(event) => update({ groupBy: event.target.value })}
              className={INPUT}
            >
              <option value="">Day (default)</option>
              {GROUP_BY_OPTIONS.filter((option) => option !== 'Day').map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </FieldShell>
        )}

        {definition.parameters.map((parameter) => (
          <ReportParameterControl
            key={parameter.name}
            idPrefix="report-param"
            parameter={parameter}
            value={draft.parameters[parameter.name] ?? ''}
            error={errors[parameter.name]}
            onChange={(value) =>
              update({ parameters: { ...draft.parameters, [parameter.name]: value } })
            }
          />
        ))}
      </div>

      <div className="flex flex-wrap justify-end gap-2">
        <Button variant="secondary" onClick={onReset}>
          Reset
        </Button>
        <Button type="submit">Apply</Button>
      </div>
    </form>
  );
}

function FieldShell({
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
        <p id={`${id}-error`} className="m-0 text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
