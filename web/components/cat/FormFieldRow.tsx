import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/cat/FormFieldRow.tsx` — a labelled `<input>` with an optional error, extracted from
 * `MedicalProfileForm` (ARCHITECTURE gate MEDIUM finding, HO-CAT-002 4th pass): that file had the
 * same `aria-invalid` + `aria-describedby` + `<p id=…-error role="alert">` block copied by hand
 * seven times, once per repeatable-row input. This is that block, generalised — the plain,
 * unitless sibling of `components/common/FieldWithUnit.tsx`, which stays a separate component
 * because it renders the unit affordance FieldWithUnit exists for and this one deliberately
 * doesn't.
 *
 * Spread `{...register(name)}` last, exactly as every existing cat form already does with
 * `FieldWithUnit` (see `WeightEntryForm.tsx`) — `register`'s own `ref`/`onChange`/`onBlur` land on
 * the underlying `<input>` through this component's `forwardRef`.
 */
export interface FormFieldRowProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'id'> {
  id: string;
  label: string;
  error?: string;
  required?: boolean;
  className?: string;
}

export const FormFieldRow = React.forwardRef<HTMLInputElement, FormFieldRowProps>(
  ({ id, label, error, required, className, type = 'text', ...inputProps }, ref) => {
    const errorId = error ? `${id}-error` : undefined;

    return (
      <div className={cn('flex flex-col gap-1.5', className)}>
        <label htmlFor={id} className="text-sm font-semibold text-ink">
          {label}
          {required && (
            <span aria-hidden="true" className="text-danger-ink">
              {' '}
              *
            </span>
          )}
        </label>
        <input
          ref={ref}
          id={id}
          type={type}
          required={required}
          aria-invalid={!!error}
          aria-describedby={errorId}
          className={cn(
            'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink',
            'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
            type === 'date' && 'w-fit',
            error && 'border-danger-ink',
          )}
          {...inputProps}
        />
        {error && (
          <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
            {error}
          </p>
        )}
      </div>
    );
  },
);
FormFieldRow.displayName = 'FormFieldRow';
