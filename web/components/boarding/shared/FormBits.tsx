import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/shared/FormBits.tsx` — the labelled `<select>`, `<textarea>` and an inline
 * error block that match `FormFieldRow` (components/cat) so every boarding form looks and reads the
 * same: label above, error below in words (`role="alert"`), `aria-invalid` + `aria-describedby`.
 */
export const fieldClass =
  'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ' +
  'disabled:bg-surface-sunken disabled:text-ink-muted';

export function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) return null;
  return (
    <p id={id} role="alert" className="text-xs font-semibold text-danger-ink">
      {message}
    </p>
  );
}

interface BaseProps {
  id: string;
  label: string;
  error?: string;
  hint?: string;
  required?: boolean;
  className?: string;
}

function Label({ id, label, required }: Pick<BaseProps, 'id' | 'label' | 'required'>) {
  return (
    <label htmlFor={id} className="text-sm font-semibold text-ink">
      {label}
      {required && (
        <span aria-hidden="true" className="text-danger-ink">
          {' '}
          *
        </span>
      )}
    </label>
  );
}

function describedBy(id: string, error?: string, hint?: string) {
  return (
    [error ? `${id}-error` : null, hint ? `${id}-hint` : null].filter(Boolean).join(' ') ||
    undefined
  );
}

export const SelectRow = React.forwardRef<
  HTMLSelectElement,
  BaseProps & React.SelectHTMLAttributes<HTMLSelectElement>
>(({ id, label, error, hint, required, className, children, ...rest }, ref) => (
  <div className={cn('flex flex-col gap-1.5', className)}>
    <Label id={id} label={label} required={required} />
    <select
      ref={ref}
      id={id}
      aria-invalid={!!error}
      aria-describedby={describedBy(id, error, hint)}
      className={cn(fieldClass, error && 'border-danger-ink')}
      {...rest}
    >
      {children}
    </select>
    {hint && (
      <p id={`${id}-hint`} className="text-xs text-ink-muted">
        {hint}
      </p>
    )}
    <FieldError id={`${id}-error`} message={error} />
  </div>
));
SelectRow.displayName = 'SelectRow';

export const TextAreaRow = React.forwardRef<
  HTMLTextAreaElement,
  BaseProps & React.TextareaHTMLAttributes<HTMLTextAreaElement>
>(({ id, label, error, hint, required, className, rows = 3, ...rest }, ref) => (
  <div className={cn('flex flex-col gap-1.5', className)}>
    <Label id={id} label={label} required={required} />
    <textarea
      ref={ref}
      id={id}
      rows={rows}
      aria-invalid={!!error}
      aria-describedby={describedBy(id, error, hint)}
      className={cn(fieldClass, 'resize-y', error && 'border-danger-ink')}
      {...rest}
    />
    {hint && (
      <p id={`${id}-hint`} className="text-xs text-ink-muted">
        {hint}
      </p>
    )}
    <FieldError id={`${id}-error`} message={error} />
  </div>
));
TextAreaRow.displayName = 'TextAreaRow';

/** A form-level server message (`root`), announced when it appears. */
export function FormErrorAlert({ message }: { message?: string | null }) {
  if (!message) return null;
  return (
    <p
      role="alert"
      className="rounded-[var(--radius-sm)] border border-border bg-danger-bg px-3 py-2 text-sm font-semibold text-danger-ink"
    >
      {message}
    </p>
  );
}
