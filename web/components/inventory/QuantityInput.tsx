import * as React from 'react';
import { quantityProblem } from '@/lib/validation/inventory';
import { cn } from '@/lib/utils';
import { inputClass } from './fields';

/**
 * `components/inventory/QuantityInput.tsx` — HO-IMP-002 §11.1, UI-8, ADR-FE-041. A decimal STRING
 * input with the stock unit shown beside it AND inside the label's accessible name (so a screen
 * reader hears "Accepted, bags"). It never converts the value to a number. It refuses a quantity
 * the SKU cannot hold — more than 3 decimals, or any decimals for a whole-only item — with a
 * unit-aware message the moment it is typed (`FractionalQuantityNotAllowed` mirrored for
 * immediacy; the server stays the authority). An externally supplied `error` wins.
 */
export interface QuantityInputProps {
  id: string;
  label: string;
  value: string;
  onValueChange: (value: string) => void;
  stockUnit: string;
  allowsFractional: boolean;
  error?: string;
  hint?: string;
  required?: boolean;
  disabled?: boolean;
  /** Allow a leading minus (counts and adjustments only). */
  signed?: boolean;
  className?: string;
  inputRef?: React.Ref<HTMLInputElement>;
  /** Fires when the field loses focus (the count sheet saves a line then). */
  onBlur?: () => void;
  /** Fires on Enter (without submitting a surrounding form). */
  onEnter?: () => void;
}

export function QuantityInput({
  id,
  label,
  value,
  onValueChange,
  stockUnit,
  allowsFractional,
  error,
  hint,
  required,
  disabled,
  signed,
  className,
  inputRef,
  onBlur,
  onEnter,
}: QuantityInputProps) {
  const probe = signed && value.startsWith('-') ? value.slice(1) : value;
  const localProblem = quantityProblem(probe, { stockUnit, allowsFractional });
  const message = error ?? localProblem ?? undefined;
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = message ? `${id}-error` : undefined;

  return (
    <div className={cn('flex min-w-0 flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label} <span className="font-normal text-ink-muted">({stockUnit})</span>
        {required && (
          <span aria-hidden="true" className="text-danger-ink">
            {' '}
            *
          </span>
        )}
      </label>
      <div className="relative">
        <input
          ref={inputRef}
          id={id}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={value}
          disabled={disabled}
          required={required}
          aria-invalid={!!message}
          aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
          onChange={(event) => onValueChange(event.target.value)}
          onBlur={onBlur}
          onKeyDown={
            onEnter
              ? (event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    onEnter();
                  }
                }
              : undefined
          }
          className={cn(inputClass, 'pr-16', message && 'border-danger-ink')}
        />
        <span
          aria-hidden="true"
          className="pointer-events-none absolute inset-y-0 right-3 flex max-w-[5.5rem] items-center truncate text-sm text-ink-muted"
        >
          {stockUnit}
        </span>
      </div>
      {hint && (
        <p id={hintId} className="text-xs text-ink-muted">
          {hint}
        </p>
      )}
      {message && (
        <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
          {message}
        </p>
      )}
    </div>
  );
}

/**
 * `MoneyInput` / `UnitCostInput` — a decimal string input with the currency (or the currency per
 * unit) shown beside it. `decimals` is 2 for money and 4 for a unit cost.
 */
export function MoneyInput({
  id,
  label,
  value,
  onValueChange,
  currencyCode,
  decimals = 2,
  perUnit,
  error,
  hint,
  required,
  disabled,
  readOnly,
  className,
}: {
  id: string;
  label: string;
  value: string;
  onValueChange: (value: string) => void;
  currencyCode: string;
  decimals?: 2 | 4;
  perUnit?: string;
  error?: string;
  hint?: string;
  required?: boolean;
  disabled?: boolean;
  readOnly?: boolean;
  className?: string;
}) {
  const suffix = perUnit ? `${currencyCode} / ${perUnit}` : currencyCode;
  const pattern = decimals === 4 ? /^\d+(\.\d{1,4})?$/ : /^\d+(\.\d{1,2})?$/;
  const local =
    value.trim() !== '' && !pattern.test(value.trim())
      ? `Enter an amount with at most ${decimals} decimal places.`
      : undefined;
  const message = error ?? local;
  const errorId = message ? `${id}-error` : undefined;
  const hintId = hint ? `${id}-hint` : undefined;
  return (
    <div className={cn('flex min-w-0 flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label} <span className="font-normal text-ink-muted">({suffix})</span>
        {required && (
          <span aria-hidden="true" className="text-danger-ink">
            {' '}
            *
          </span>
        )}
      </label>
      <div className="relative">
        <input
          id={id}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={value}
          disabled={disabled}
          readOnly={readOnly}
          required={required}
          aria-invalid={!!message}
          aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
          onChange={(event) => onValueChange(event.target.value)}
          className={cn(inputClass, 'pr-20', message && 'border-danger-ink', readOnly && 'bg-surface-sunken')}
        />
        <span
          aria-hidden="true"
          className="pointer-events-none absolute inset-y-0 right-3 flex max-w-[6.5rem] items-center truncate text-sm text-ink-muted"
        >
          {suffix}
        </span>
      </div>
      {hint && (
        <p id={hintId} className="text-xs text-ink-muted">
          {hint}
        </p>
      )}
      {message && (
        <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
          {message}
        </p>
      )}
    </div>
  );
}
