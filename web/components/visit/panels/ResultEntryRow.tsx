'use client';

import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { MeasurementInput, sanitiseMeasurement } from '../MeasurementInput';
import { FlagBadge } from '../FlagBadge';
import {
  MAX_NORMAL_TEXT,
  MAX_TEXT_VALUE,
  MAX_UNIT,
  MAX_VALUE_NAME,
  previewNumericFlag,
  previewQualitativeFlag,
} from '@/lib/validation/clinical';
import type { ResultFlag, ResultValueKind } from '@/lib/api/diagnostics';

/**
 * `ResultEntryRow` — one value of `ResultEntryForm` (HO-EMR-002 §11.3). An analyte pre-fills the name,
 * unit and range but every field stays editable; the value is a `MeasurementInput` whose suffix is the
 * row's own unit, and the range bounds are labelled with that unit too (UI-8). The **preview flag** is
 * the same rule the server applies (`ReferenceRangeClassifier`) for immediate feedback — labelled a
 * preview, and replaced by the server's flag once saved.
 */
export interface ResultRowState {
  key: string;
  analyteId?: string;
  name: string;
  valueKind: ResultValueKind;
  value: string;
  unit: string;
  low: string;
  high: string;
  normalText: string;
}

export function previewFlagFor(row: ResultRowState): ResultFlag | null {
  if (row.valueKind === 'Qualitative') {
    return row.value.trim() ? previewQualitativeFlag(row.value, row.normalText) : null;
  }
  if (row.value === '' || row.value === '.') return null;
  const low = row.low === '' || row.low === '.' ? null : Number(row.low);
  const high = row.high === '' || row.high === '.' ? null : Number(row.high);
  return previewNumericFlag(Number(row.value), low, high);
}

const inputClass =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

export function ResultEntryRow({
  row,
  index,
  onChange,
  onRemove,
  error,
}: {
  row: ResultRowState;
  index: number;
  onChange: (patch: Partial<ResultRowState>) => void;
  onRemove: () => void;
  error?: string;
}) {
  const flag = previewFlagFor(row);
  const id = `result-row-${row.key}`;
  const numeric = row.valueKind === 'Numeric';
  const unit = row.unit.trim() || 'unit';
  return (
    <fieldset className="m-0 flex flex-col gap-3 rounded-[var(--radius-sm)] border border-border p-3">
      <legend className="px-1 text-xs font-semibold text-ink-muted">Value {index + 1}</legend>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-40 flex-1 flex-col gap-1.5">
          <label htmlFor={`${id}-name`} className="text-sm font-semibold text-ink">
            Test name
          </label>
          <input
            id={`${id}-name`}
            type="text"
            maxLength={MAX_VALUE_NAME}
            value={row.name}
            onChange={(e) => onChange({ name: e.target.value })}
            className={inputClass}
          />
        </div>
        <fieldset className="m-0 flex gap-3 border-0 p-0">
          <legend className="sr-only">Kind of result for value {index + 1}</legend>
          {(['Numeric', 'Qualitative'] as ResultValueKind[]).map((kind) => (
            <label key={kind} className="inline-flex items-center gap-1.5 text-sm text-ink">
              <input
                type="radio"
                name={`${id}-kind`}
                checked={row.valueKind === kind}
                onChange={() => onChange({ valueKind: kind })}
              />
              {kind === 'Numeric' ? 'Number' : 'Text result'}
            </label>
          ))}
        </fieldset>
        <Button
          variant="ghost"
          size="sm"
          onClick={onRemove}
          aria-label={`Remove value ${index + 1}`}
        >
          <X size={14} aria-hidden="true" /> Remove
        </Button>
      </div>

      {numeric ? (
        <div className="grid gap-3 min-[460px]:grid-cols-2">
          <MeasurementInput
            id={`${id}-value`}
            label="Result"
            unitName={unit}
            unitSymbol={unit}
            decimalPlaces={4}
            value={row.value}
            onChange={(value) => onChange({ value })}
          />
          <div className="flex flex-col gap-1.5">
            <label htmlFor={`${id}-unit`} className="text-sm font-semibold text-ink">
              Unit
            </label>
            <input
              id={`${id}-unit`}
              type="text"
              maxLength={MAX_UNIT}
              value={row.unit}
              onChange={(e) => onChange({ unit: e.target.value })}
              className={inputClass}
            />
          </div>
          <MeasurementInput
            id={`${id}-low`}
            label="Range low"
            unitName={unit}
            unitSymbol={unit}
            decimalPlaces={4}
            value={row.low}
            onChange={(low) => onChange({ low })}
          />
          <MeasurementInput
            id={`${id}-high`}
            label="Range high"
            unitName={unit}
            unitSymbol={unit}
            decimalPlaces={4}
            value={row.high}
            onChange={(high) => onChange({ high: sanitiseMeasurement(high, 4) })}
          />
        </div>
      ) : (
        <div className="grid gap-3 min-[460px]:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <label htmlFor={`${id}-text`} className="text-sm font-semibold text-ink">
              Result
            </label>
            <input
              id={`${id}-text`}
              type="text"
              maxLength={MAX_TEXT_VALUE}
              value={row.value}
              onChange={(e) => onChange({ value: e.target.value })}
              className={inputClass}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor={`${id}-normal`} className="text-sm font-semibold text-ink">
              Normal result
            </label>
            <input
              id={`${id}-normal`}
              type="text"
              maxLength={MAX_NORMAL_TEXT}
              value={row.normalText}
              onChange={(e) => onChange({ normalText: e.target.value })}
              className={inputClass}
            />
          </div>
        </div>
      )}

      <p className="flex flex-wrap items-center gap-2 text-sm text-ink-muted" aria-live="polite">
        Preview flag: {flag ? <FlagBadge flag={flag} /> : <span>enter a result to see it</span>}
        <span className="text-xs">The server confirms the flag when you save.</span>
      </p>
      {error && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </fieldset>
  );
}
