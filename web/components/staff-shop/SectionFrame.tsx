'use client';

import * as React from 'react';
import type { FieldErrors } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';

export interface SectionFrameProps {
  title: string;
  description?: React.ReactNode;
  errors: FieldErrors;
  isDirty: boolean;
  isSubmitting: boolean;
  isSubmitSuccessful?: boolean;
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void;
  /** Reloads the product after a `409`; the typed values stay in the form. */
  onReload: () => void;
  saveLabel?: string;
  children: React.ReactNode;
}

/**
 * HO-SHP-002 §11.6, ADR-FE-034 — the shape every editor section shares: one `<form>` that saves on
 * its own, a root error area (a `409` offers **Reload** and keeps the form's values — NFR-USA-5), the
 * `UnsavedChangesGuard`, and a Save button that is never disabled to express invalidity.
 */
export function SectionFrame({
  title,
  description,
  errors,
  isDirty,
  isSubmitting,
  isSubmitSuccessful,
  onSubmit,
  onReload,
  saveLabel = 'Save',
  children,
}: SectionFrameProps) {
  const root = errors.root;
  return (
    <form onSubmit={onSubmit} noValidate className="flex max-w-3xl flex-col gap-4">
      <UnsavedChangesGuard when={isDirty && !isSubmitting && !isSubmitSuccessful} />
      <h2 className="font-display text-xl font-semibold text-ink">{title}</h2>
      {description && <div className="text-sm text-ink-muted">{description}</div>}
      {root?.message && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          <span>{String(root.message)}</span>
          {root.type === 'conflict' && (
            <Button type="button" variant="secondary" size="sm" onClick={onReload}>
              Reload
            </Button>
          )}
        </div>
      )}
      {children}
      <Button type="submit" disabled={isSubmitting} className="self-start">
        {isSubmitting ? 'Saving…' : saveLabel}
      </Button>
    </form>
  );
}

export function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return (
    <p role="alert" className="text-xs font-semibold text-danger-ink">
      {message}
    </p>
  );
}

export const inputClass =
  'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink aria-[invalid=true]:border-danger-ink';

export function Field({
  id,
  label,
  error,
  hint,
  children,
}: {
  id: string;
  label: React.ReactNode;
  error?: string;
  hint?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
      </label>
      {children}
      {hint && <p className="text-xs text-ink-muted">{hint}</p>}
      <FieldError message={error} />
    </div>
  );
}
