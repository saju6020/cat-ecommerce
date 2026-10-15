'use client';

import * as React from 'react';
import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { inputClass } from '@/components/inventory/fields';
import { cn } from '@/lib/utils';

/**
 * `ChipsField` — a short list of terms (active ingredients, drug classes). Type, then Enter or **Add**;
 * each chip has its own *Remove* button named after the term. At `max` the box is disabled and says why.
 */
export function ChipsField({
  id,
  label,
  values,
  onChange,
  max,
  hint,
  error,
  required,
}: {
  id: string;
  label: string;
  values: string[];
  onChange: (next: string[]) => void;
  max: number;
  hint?: string;
  error?: string;
  required?: boolean;
}) {
  const [draft, setDraft] = React.useState('');
  const full = values.length >= max;

  function add() {
    const term = draft.trim();
    if (!term || full) return;
    if (!values.some((value) => value.toLowerCase() === term.toLowerCase()))
      onChange([...values, term]);
    setDraft('');
  }

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-ink">
        {label}
        {required && (
          <span aria-hidden="true" className="text-danger-ink">
            {' '}
            *
          </span>
        )}
      </label>
      <div className="flex gap-2">
        <input
          id={id}
          type="text"
          autoComplete="off"
          value={draft}
          disabled={full}
          aria-invalid={!!error}
          aria-describedby={[`${id}-hint`, error ? `${id}-error` : ''].filter(Boolean).join(' ')}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add();
            }
          }}
          className={cn(inputClass, error && 'border-danger-ink')}
        />
        <Button variant="secondary" onClick={add} disabled={full || draft.trim() === ''}>
          Add
        </Button>
      </div>
      <p id={`${id}-hint`} className="text-xs text-ink-muted">
        {full ? `You can list at most ${max}.` : hint}
      </p>
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {values.length > 0 && (
        <ul className="flex list-none flex-wrap gap-2 p-0">
          {values.map((value) => (
            <li
              key={value}
              className="inline-flex items-center gap-1 rounded-full bg-surface-sunken px-2.5 py-1 text-sm text-ink"
            >
              {value}
              <button
                type="button"
                aria-label={`Remove ${value}`}
                onClick={() => onChange(values.filter((v) => v !== value))}
                className="rounded-full p-0.5 text-ink-muted hover:text-ink focus-visible:outline-2 focus-visible:outline-accent"
              >
                <X size={12} aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
