import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export const catalogueInputClass =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:bg-surface-sunken disabled:text-ink-muted';

/**
 * `CatalogueField` — a labelled form row for the clinical catalogue editors (HO-EMR-002 §11.8): the
 * label, the control (passed as a render function so it gets the ids), an optional hint and the
 * field's server/client error, wired with `aria-describedby` / `aria-invalid`.
 */
export function CatalogueField({
  id,
  label,
  hint,
  error,
  className,
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string;
  className?: string;
  children: (props: {
    id: string;
    'aria-invalid'?: true;
    'aria-describedby'?: string;
    className: string;
  }) => ReactNode;
}) {
  const describedBy =
    [hint ? `${id}-hint` : '', error ? `${id}-error` : ''].filter(Boolean).join(' ') || undefined;
  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
      </label>
      {children({
        id,
        ...(error ? { 'aria-invalid': true as const } : {}),
        'aria-describedby': describedBy,
        className: catalogueInputClass,
      })}
      {hint && (
        <p id={`${id}-hint`} className="text-xs text-ink-muted">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
