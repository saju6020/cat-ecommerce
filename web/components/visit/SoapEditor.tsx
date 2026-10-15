'use client';

import type { UseFormReturn } from 'react-hook-form';
import { cn } from '@/lib/utils';
import {
  MAX_DISCHARGE_INSTRUCTIONS,
  MAX_SOAP_FIELD,
  type NoteFormValues,
} from '@/lib/validation/clinical';

/**
 * ⭐ `SoapEditor` — HO-EMR-002 §11.1 (US-CLN-12). Four labelled textareas (Subjective, Objective,
 * Assessment, Plan) plus Home-care instructions, each with a character counter at the SERVER'S limit
 * (`ClinicalNoteRules`: 20,000 / 10,000). Plain text only (ADR-FE-058). The field ids are the targets
 * of the finalise checklist's links. Initial values come from the parent's once-per-load
 * initialisation — a refetch never touches what is typed here.
 */
const FIELDS: Array<{
  name: 'subjective' | 'objective' | 'assessment' | 'plan' | 'dischargeInstructions';
  id: string;
  label: string;
  hint?: string;
  max: number;
  rows: number;
}> = [
  { name: 'subjective', id: 'note-subjective', label: 'Subjective', max: MAX_SOAP_FIELD, rows: 4 },
  { name: 'objective', id: 'note-objective', label: 'Objective', max: MAX_SOAP_FIELD, rows: 4 },
  { name: 'assessment', id: 'note-assessment', label: 'Assessment', max: MAX_SOAP_FIELD, rows: 3 },
  { name: 'plan', id: 'note-plan', label: 'Plan', max: MAX_SOAP_FIELD, rows: 3 },
  {
    name: 'dischargeInstructions',
    id: 'note-discharge',
    label: 'Home-care instructions',
    hint: 'Shown to the owner on the discharge summary.',
    max: MAX_DISCHARGE_INSTRUCTIONS,
    rows: 4,
  },
];

export function SoapEditor({
  form,
  fieldErrors = {},
}: {
  form: UseFormReturn<NoteFormValues>;
  fieldErrors?: Partial<Record<string, string>>;
}) {
  const values = form.watch();
  return (
    <section
      aria-labelledby="soap-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="soap-heading" className="text-sm font-bold uppercase tracking-wide text-ink-muted">
          Note (SOAP)
        </h2>
        <span className="text-xs text-ink-muted">Ctrl+S saves the note</span>
      </div>
      {FIELDS.map((field) => {
        const length = values[field.name]?.length ?? 0;
        const error = fieldErrors[field.name];
        return (
          <div key={field.id} className="flex flex-col gap-1.5">
            <label htmlFor={field.id} className="text-sm font-semibold text-ink">
              {field.label}
            </label>
            {field.hint && (
              <p id={`${field.id}-hint`} className="text-xs text-ink-muted">
                {field.hint}
              </p>
            )}
            <textarea
              id={field.id}
              rows={field.rows}
              aria-invalid={error ? true : undefined}
              aria-describedby={
                [
                  field.hint ? `${field.id}-hint` : '',
                  `${field.id}-count`,
                  error ? `${field.id}-error` : '',
                ]
                  .filter(Boolean)
                  .join(' ') || undefined
              }
              className={cn(
                'w-full resize-y rounded-[var(--radius-sm)] border bg-surface px-3 py-2 text-sm leading-relaxed text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                error ? 'border-danger-ink' : 'border-border-strong',
              )}
              {...form.register(field.name)}
            />
            <p
              id={`${field.id}-count`}
              className={cn(
                'text-right text-xs',
                length > field.max ? 'font-semibold text-danger-ink' : 'text-ink-muted',
              )}
            >
              {length} of {field.max} characters
            </p>
            {error && (
              <p
                id={`${field.id}-error`}
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {error}
              </p>
            )}
          </div>
        );
      })}
    </section>
  );
}
