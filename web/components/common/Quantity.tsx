import * as React from 'react';
import {
  formatQuantity,
  formatSignedQuantity,
  unitLabel,
  type QuantityValue,
} from '@/lib/format/units';
import { cn } from '@/lib/utils';

/**
 * `components/common/Quantity.tsx` — HO-PHM-002 §11.1, ADR-FE-061, UI-8. The ONLY way a dose, quantity,
 * weight, strength or range is rendered on a pharmacy screen: number and unit as ONE text run
 * ("0.30 ml") with a non-breaking space, so the unit can never wrap away from its number or be
 * omitted. `data-clinical-value` lets the U-X1 test find every clinical number on a screen.
 *
 * Deviation recorded in HO-PHM-002 §1: no `aria-label` spelling the unit. A label on a plain `<span>`
 * has no role to name, which axe reports; the visible text "0.30 ml" is already read with its unit.
 */
export function Quantity({
  value,
  as: Tag = 'span',
  className,
}: {
  value: QuantityValue;
  as?: 'span' | 'strong' | 'b' | 'div' | 'dd';
  className?: string;
}) {
  return (
    <Tag data-clinical-value className={cn('whitespace-nowrap', className)}>
      {formatQuantity(value)}
    </Tag>
  );
}

/** A register movement: always signed ("+10.00 ml", "−2.00 ml"), in one text run with its unit. */
export function SignedQuantity({ value, className }: { value: QuantityValue; className?: string }) {
  return (
    <span data-clinical-value className={cn('whitespace-nowrap', className)}>
      {formatSignedQuantity(value)}
    </span>
  );
}

/** A free-text clinical figure the server already formatted ("0.05–0.10 mg/kg"), marked the same way. */
export function ClinicalText({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <span data-clinical-value className={className}>
      {children}
    </span>
  );
}

const FIELD =
  'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

/**
 * `QuantityInput` — a number field with its unit beside it (never only in a placeholder). With
 * `units` the unit is a `<select>` that starts empty — **no default dose unit** (the vet chooses);
 * with `fixedUnit` the unit is fixed text (a quantity in the medicine's dispensing unit). The
 * label's accessible name includes the unit when it is fixed.
 */
export interface QuantityInputProps {
  id: string;
  label: string;
  amount: string;
  unit: string;
  onChange: (next: { amount: string; unit: string }) => void;
  /** Offered units (choose one). Omit with `fixedUnit`. */
  units?: string[];
  /** A unit that cannot be changed. */
  fixedUnit?: string;
  hint?: string;
  error?: string;
  required?: boolean;
  disabled?: boolean;
}

export function QuantityInput({
  id,
  label,
  amount,
  unit,
  onChange,
  units,
  fixedUnit,
  hint,
  error,
  required,
  disabled,
}: QuantityInputProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
        {fixedUnit && (
          <span className="font-normal text-ink-muted"> ({unitLabel(fixedUnit, '2')})</span>
        )}
        {required && (
          <span aria-hidden="true" className="text-danger-ink">
            {' '}
            *
          </span>
        )}
      </label>
      <div className="flex gap-2">
        <input
          id={id}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={amount}
          disabled={disabled}
          required={required}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          onChange={(event) => onChange({ amount: event.target.value, unit })}
          className={cn(FIELD, 'w-28 min-w-0', error && 'border-danger-ink')}
        />
        {units ? (
          <select
            aria-label={`${label} unit`}
            value={unit}
            disabled={disabled}
            aria-invalid={!!error}
            onChange={(event) => onChange({ amount, unit: event.target.value })}
            className={cn(FIELD, 'min-w-0 flex-1', error && 'border-danger-ink')}
          >
            <option value="">Choose unit</option>
            {units.map((option) => (
              <option key={option} value={option}>
                {unitLabel(option, '2')}
              </option>
            ))}
          </select>
        ) : (
          <span className="flex items-center text-sm text-ink-muted">
            {unitLabel(fixedUnit ?? unit, '2')}
          </span>
        )}
      </div>
      {hint && (
        <p id={hintId} className="text-xs text-ink-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
