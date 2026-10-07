import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/common/FieldWithUnit.tsx` — HO-CAT-002 §11.7, UI-8: "Unit rendered adjacent and
 * always visible, inside the label's accessible name." Used for weight entry, weight-chart axes,
 * medication dosage (CAT), and every stock quantity (`formatQuantity`, HO-IMP-002 §8.4).
 *
 * The unit appears twice, once each for a different reason:
 *  - inside the `<label>` itself, so it is part of the input's accessible name for a screen
 *    reader ("Weight, kg" — not just "Weight");
 *  - as a visible `aria-hidden` suffix beside the input, so a sighted user reads it at a glance
 *    without it being announced twice.
 *
 * A bare numeric input with no unit anywhere is what UI-8 exists to prevent — do not use a plain
 * `<input>` for a weight, dose or quantity field; use this.
 */
export interface FieldWithUnitProps extends Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  'id' | 'type'
> {
  id: string;
  label: string;
  /** e.g. "kg", "mL", "bags" — shown adjacent to the input and folded into the label's
   *  accessible name. */
  unit: string;
  hint?: string;
  error?: string;
  required?: boolean;
  className?: string;
}

export const FieldWithUnit = React.forwardRef<HTMLInputElement, FieldWithUnitProps>(
  ({ id, label, unit, hint, error, required, className, ...inputProps }, ref) => {
    const hintId = hint ? `${id}-hint` : undefined;
    const errorId = error ? `${id}-error` : undefined;

    return (
      <div className={cn('flex flex-col gap-1.5', className)}>
        <label htmlFor={id} className="text-sm font-semibold text-ink">
          {label} <span className="font-normal text-ink-muted">({unit})</span>
          {required && (
            <span aria-hidden="true" className="text-danger-ink">
              {' '}
              *
            </span>
          )}
        </label>
        <div className="relative">
          <input
            ref={ref}
            id={id}
            type="number"
            inputMode="decimal"
            required={required}
            aria-invalid={!!error}
            aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
            className={cn(
              'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface py-2 pl-3 pr-14 text-sm text-ink',
              'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
              error && 'border-danger-ink',
            )}
            {...inputProps}
          />
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-ink-muted"
          >
            {unit}
          </span>
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
  },
);
FieldWithUnit.displayName = 'FieldWithUnit';
