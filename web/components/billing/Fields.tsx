'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/billing/Fields.tsx` — HO-BIL-002 §7 (NFR-USA-4/5, UI-6). Labelled form controls for the
 * Billing screens: a visible label, the server's message under the field (`role="alert"`, wired with
 * `aria-describedby`), and a hint. They spread `{...register(name)}` last, like `FormFieldRow`.
 * `MoneyField` is a text input in decimal mode — never `type="number"`, so the value stays the exact
 * string the person typed and no browser parsing is involved (ADR-FE-080).
 */
const CONTROL =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-60';

interface ShellProps {
  id: string;
  label: string;
  hint?: React.ReactNode;
  error?: string;
  required?: boolean;
  className?: string;
  children: (describedBy: string | undefined) => React.ReactNode;
}

export function FieldShell({ id, label, hint, error, required, className, children }: ShellProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;
  return (
    <div className={cn('flex min-w-0 flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
        {required && (
          <span aria-hidden="true" className="text-danger-ink">
            {' '}
            *
          </span>
        )}
      </label>
      {children(describedBy)}
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

export interface TextFieldProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'id'> {
  id: string;
  label: string;
  hint?: React.ReactNode;
  error?: string;
  required?: boolean;
  wrapperClassName?: string;
}

export const TextField = React.forwardRef<HTMLInputElement, TextFieldProps>(
  ({ id, label, hint, error, required, wrapperClassName, className, ...input }, ref) => (
    <FieldShell
      id={id}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      {(describedBy) => (
        <input
          ref={ref}
          id={id}
          required={required}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          className={cn(CONTROL, error && 'border-danger-ink', className)}
          {...input}
        />
      )}
    </FieldShell>
  ),
);
TextField.displayName = 'TextField';

/** A money amount typed as text (`inputMode="decimal"`); the currency code sits beside the label. */
export const MoneyField = React.forwardRef<
  HTMLInputElement,
  Omit<TextFieldProps, 'type' | 'inputMode'> & { currencyCode?: string }
>(({ label, currencyCode = 'BDT', ...props }, ref) => (
  <TextField
    ref={ref}
    label={`${label} (${currencyCode})`}
    type="text"
    inputMode="decimal"
    autoComplete="off"
    spellCheck={false}
    {...props}
  />
));
MoneyField.displayName = 'MoneyField';

export interface TextAreaFieldProps extends Omit<
  React.TextareaHTMLAttributes<HTMLTextAreaElement>,
  'id'
> {
  id: string;
  label: string;
  hint?: React.ReactNode;
  error?: string;
  required?: boolean;
  wrapperClassName?: string;
}

export const TextAreaField = React.forwardRef<HTMLTextAreaElement, TextAreaFieldProps>(
  ({ id, label, hint, error, required, wrapperClassName, className, ...input }, ref) => (
    <FieldShell
      id={id}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      {(describedBy) => (
        <textarea
          ref={ref}
          id={id}
          rows={3}
          required={required}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          className={cn(CONTROL, error && 'border-danger-ink', className)}
          {...input}
        />
      )}
    </FieldShell>
  ),
);
TextAreaField.displayName = 'TextAreaField';

export interface SelectFieldProps extends Omit<
  React.SelectHTMLAttributes<HTMLSelectElement>,
  'id'
> {
  id: string;
  label: string;
  hint?: React.ReactNode;
  error?: string;
  required?: boolean;
  wrapperClassName?: string;
}

export const SelectField = React.forwardRef<HTMLSelectElement, SelectFieldProps>(
  ({ id, label, hint, error, required, wrapperClassName, className, children, ...input }, ref) => (
    <FieldShell
      id={id}
      label={label}
      hint={hint}
      error={error}
      required={required}
      className={wrapperClassName}
    >
      {(describedBy) => (
        <select
          ref={ref}
          id={id}
          required={required}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          className={cn(CONTROL, error && 'border-danger-ink', className)}
          {...input}
        >
          {children}
        </select>
      )}
    </FieldShell>
  ),
);
SelectField.displayName = 'SelectField';

/** A form-level (non-field) message — the server's, plain-language (NFR-USA-4). */
export function FormRootError({ message }: { message?: string }) {
  if (!message) return null;
  return (
    <p role="alert" className="text-sm font-semibold text-danger-ink">
      {message}
    </p>
  );
}
