'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * `MeasurementInput` — HO-EMR-002 §11.2, ADR-FE-053, UI-8. A numeric input with a PERMANENT visible
 * unit suffix. The unit is part of the accessible name (*"Weight, kilograms"*), the suffix itself is
 * `aria-hidden` so a screen reader does not hear it twice. Text input with `inputMode="decimal"` — no
 * spinner (a spinner nudges a clinical number by accident); typing is filtered to digits and one
 * decimal point, limited to the sign's `decimalPlaces` from the server's rules. The value is a STRING
 * the caller owns — never re-formatted through a float.
 */
export interface MeasurementInputProps {
  id: string;
  /** The measured thing, e.g. `Weight`. */
  label: string;
  /** The unit spelled out for the accessible name, e.g. `kilograms`. */
  unitName: string;
  /** The compact suffix shown beside the field, e.g. `kg`. */
  unitSymbol: string;
  value: string;
  onChange: (value: string) => void;
  decimalPlaces?: number;
  /** An early, informational hint (e.g. the plausible range) — announced politely when it appears. */
  warning?: string;
  error?: string;
  disabled?: boolean;
  className?: string;
}

export function sanitiseMeasurement(raw: string, decimalPlaces: number): string {
  let cleaned = raw.replace(/[^\d.]/g, '');
  const firstDot = cleaned.indexOf('.');
  if (firstDot !== -1) {
    cleaned = cleaned.slice(0, firstDot + 1) + cleaned.slice(firstDot + 1).replace(/\./g, '');
  }
  if (decimalPlaces === 0) return cleaned.replace(/\./g, '');
  const [whole, fraction] = cleaned.split('.');
  return fraction === undefined ? cleaned : `${whole}.${fraction.slice(0, decimalPlaces)}`;
}

export const MeasurementInput = React.forwardRef<HTMLInputElement, MeasurementInputProps>(
  (
    {
      id,
      label,
      unitName,
      unitSymbol,
      value,
      onChange,
      decimalPlaces = 2,
      warning,
      error,
      disabled,
      className,
    },
    ref,
  ) => {
    const warningId = warning ? `${id}-warning` : undefined;
    const errorId = error ? `${id}-error` : undefined;
    return (
      <div className={cn('flex min-w-0 flex-col gap-1.5', className)}>
        <label htmlFor={id} className="text-sm font-semibold text-ink">
          {label}
          <span className="sr-only">, {unitName}</span>
        </label>
        <div
          className={cn(
            'flex min-w-0 items-stretch overflow-hidden rounded-[var(--radius-sm)] border bg-surface',
            error ? 'border-danger-ink' : warning ? 'border-warning-ink' : 'border-border-strong',
            'focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent',
          )}
        >
          <input
            ref={ref}
            id={id}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={value}
            disabled={disabled}
            aria-invalid={error ? true : undefined}
            aria-describedby={[warningId, errorId].filter(Boolean).join(' ') || undefined}
            onChange={(event) => onChange(sanitiseMeasurement(event.target.value, decimalPlaces))}
            className="min-w-0 flex-1 bg-transparent px-3 py-2 text-sm text-ink outline-none disabled:opacity-60"
          />
          <span
            aria-hidden="true"
            className="flex flex-none items-center border-l border-border bg-surface-sunken px-2.5 text-sm font-semibold text-ink-muted"
          >
            {unitSymbol}
          </span>
        </div>
        {warning && (
          <p id={warningId} role="status" className="text-xs font-semibold text-warning-ink">
            {warning}
          </p>
        )}
        {error && (
          <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
            {error}
          </p>
        )}
      </div>
    );
  },
);
MeasurementInput.displayName = 'MeasurementInput';
