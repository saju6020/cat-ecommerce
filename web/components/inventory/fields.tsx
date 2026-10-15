import * as React from 'react';
import { cn } from '@/lib/utils';

/**
 * `components/inventory/fields.tsx` — HO-IMP-002. The labelled inputs every Inventory and
 * Procurement form shares: label, optional hint, an inline error (`role="alert"`, linked with
 * `aria-describedby`) and the same focus ring the rest of the staff app uses. Nothing here knows
 * about stock — it is layout and accessibility only.
 */

export const inputClass =
  'w-full min-w-0 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:bg-surface-sunken disabled:text-ink-muted';

interface ShellProps {
  id: string;
  label: React.ReactNode;
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

type CommonProps = {
  id: string;
  label: React.ReactNode;
  hint?: React.ReactNode;
  error?: string;
  wrapperClassName?: string;
};

export const TextField = React.forwardRef<
  HTMLInputElement,
  CommonProps & Omit<React.InputHTMLAttributes<HTMLInputElement>, 'id'>
>(({ id, label, hint, error, wrapperClassName, required, className, ...input }, ref) => (
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
        aria-invalid={!!error}
        aria-describedby={describedBy}
        required={required}
        className={cn(inputClass, error && 'border-danger-ink', className)}
        {...input}
      />
    )}
  </FieldShell>
));
TextField.displayName = 'TextField';

export const SelectField = React.forwardRef<
  HTMLSelectElement,
  CommonProps & Omit<React.SelectHTMLAttributes<HTMLSelectElement>, 'id'>
>(({ id, label, hint, error, wrapperClassName, required, className, children, ...select }, ref) => (
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
        aria-invalid={!!error}
        aria-describedby={describedBy}
        required={required}
        className={cn(inputClass, error && 'border-danger-ink', className)}
        {...select}
      >
        {children}
      </select>
    )}
  </FieldShell>
));
SelectField.displayName = 'SelectField';

export const TextAreaField = React.forwardRef<
  HTMLTextAreaElement,
  CommonProps & Omit<React.TextareaHTMLAttributes<HTMLTextAreaElement>, 'id'>
>(({ id, label, hint, error, wrapperClassName, required, className, ...area }, ref) => (
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
        aria-invalid={!!error}
        aria-describedby={describedBy}
        required={required}
        className={cn(inputClass, error && 'border-danger-ink', className)}
        {...area}
      />
    )}
  </FieldShell>
));
TextAreaField.displayName = 'TextAreaField';

export const CheckboxField = React.forwardRef<
  HTMLInputElement,
  { id: string; label: React.ReactNode; hint?: React.ReactNode } & Omit<
    React.InputHTMLAttributes<HTMLInputElement>,
    'id' | 'type'
  >
>(({ id, label, hint, className, ...input }, ref) => (
  <div className={cn('flex flex-col gap-0.5', className)}>
    <label htmlFor={id} className="flex items-start gap-2 text-sm text-ink">
      <input
        ref={ref}
        id={id}
        type="checkbox"
        aria-describedby={hint ? `${id}-hint` : undefined}
        className="mt-0.5 h-4 w-4 flex-none accent-[var(--accent)]"
        {...input}
      />
      <span>{label}</span>
    </label>
    {hint && (
      <p id={`${id}-hint`} className="pl-6 text-xs text-ink-muted">
        {hint}
      </p>
    )}
  </div>
));
CheckboxField.displayName = 'CheckboxField';

export type BannerTone = 'info' | 'warning' | 'danger' | 'neutral';

const BANNER_TONES: Record<BannerTone, string> = {
  info: 'bg-accent-soft text-accent-ink',
  warning: 'bg-warning-bg text-warning-ink',
  danger: 'bg-danger-bg text-danger-ink',
  neutral: 'bg-surface-sunken text-ink-muted',
};

/** A statement, not a toast: `role="status"` (or `alert` for a danger). */
export function Banner({
  tone = 'info',
  children,
  className,
  role,
}: {
  tone?: BannerTone;
  children: React.ReactNode;
  className?: string;
  role?: 'status' | 'alert' | 'none';
}) {
  const resolvedRole = role ?? (tone === 'danger' ? 'alert' : 'status');
  return (
    <div
      role={resolvedRole === 'none' ? undefined : resolvedRole}
      className={cn('rounded-[var(--radius-sm)] px-3 py-2 text-sm', BANNER_TONES[tone], className)}
    >
      {children}
    </div>
  );
}

export function PageHeading({
  title,
  description,
  actions,
}: {
  title: string;
  description?: React.ReactNode;
  actions?: React.ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <h1 className="font-display text-xl font-semibold text-ink">{title}</h1>
        {description && <p className="mt-1 max-w-2xl text-sm text-ink-muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

/** A bordered, titled group for a detail page. */
export function Panel({
  title,
  children,
  actions,
  className,
  headingId,
}: {
  title: string;
  children: React.ReactNode;
  actions?: React.ReactNode;
  className?: string;
  headingId?: string;
}) {
  return (
    <section
      aria-labelledby={headingId}
      className={cn(
        'min-w-0 rounded-[var(--radius-md)] border border-border bg-surface p-4',
        className,
      )}
    >
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 id={headingId} className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          {title}
        </h2>
        {actions}
      </div>
      {children}
    </section>
  );
}

/** Label/value pairs as a definition list (stacks on a phone). */
export function DefinitionList({
  items,
  className,
}: {
  items: Array<{ label: string; value: React.ReactNode }>;
  className?: string;
}) {
  return (
    <dl className={cn('grid gap-x-4 gap-y-2 text-sm sm:grid-cols-[minmax(8rem,12rem)_1fr]', className)}>
      {items.map((item) => (
        <React.Fragment key={item.label}>
          <dt className="text-ink-muted">{item.label}</dt>
          <dd className="min-w-0 break-words text-ink">{item.value}</dd>
        </React.Fragment>
      ))}
    </dl>
  );
}
