'use client';

import * as React from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * `components/auth/PasswordField.tsx` — HO-ACC-002 §11.3/§8.6.
 *  - `purpose="new"`: shows the requirements text *before typing*, linked by `aria-describedby`
 *    ("At least 10 characters. Common and previously-breached passwords are not accepted."),
 *    `autoComplete="new-password"`.
 *  - `purpose="current"`: no requirements text (there is nothing to satisfy — it is being
 *    checked, not chosen), `autoComplete="current-password"`.
 *  - Show/hide is a real `<button>` whose accessible name reflects its state ("Show password" /
 *    "Hide password"), never an icon-only control with a static name.
 */
export interface PasswordFieldProps extends Omit<
  React.InputHTMLAttributes<HTMLInputElement>,
  'id' | 'type' | 'autoComplete'
> {
  id: string;
  label: string;
  purpose: 'new' | 'current';
  error?: string;
  required?: boolean;
}

export const PasswordField = React.forwardRef<HTMLInputElement, PasswordFieldProps>(
  ({ id, label, purpose, error, required, className, ...inputProps }, ref) => {
    const [visible, setVisible] = React.useState(false);
    const hintId = purpose === 'new' ? `${id}-hint` : undefined;
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
        <div className="relative">
          <input
            ref={ref}
            id={id}
            type={visible ? 'text' : 'password'}
            required={required}
            autoComplete={purpose === 'new' ? 'new-password' : 'current-password'}
            aria-invalid={!!error}
            aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
            className={cn(
              'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface py-2 pl-3 pr-11 text-sm text-ink',
              'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
              error && 'border-danger-ink',
            )}
            {...inputProps}
          />
          <button
            type="button"
            onClick={() => setVisible((current) => !current)}
            aria-label={visible ? 'Hide password' : 'Show password'}
            className="absolute inset-y-0 right-2 flex items-center rounded-[var(--radius-sm)] p-1.5 text-ink-muted hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            {visible ? (
              <EyeOff size={16} aria-hidden="true" />
            ) : (
              <Eye size={16} aria-hidden="true" />
            )}
          </button>
        </div>
        {purpose === 'new' && (
          <p id={hintId} className="text-xs text-ink-muted">
            At least 10 characters. Common and previously-breached passwords are not accepted.
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
PasswordField.displayName = 'PasswordField';
