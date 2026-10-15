'use client';

import * as React from 'react';
import { inputClass } from './fields';
import { cn } from '@/lib/utils';

export type FilterField =
  | { name: string; label: string; type: 'text'; placeholder?: string }
  | { name: string; label: string; type: 'date' }
  | { name: string; label: string; type: 'checkbox' }
  | { name: string; label: string; type: 'number'; min?: number; max?: number }
  | {
      name: string;
      label: string;
      type: 'select';
      options: ReadonlyArray<{ value: string; label: string }>;
    };

/**
 * `components/inventory/FilterBar.tsx` — HO-IMP-002 UI-4. The filter row above a list. Controlled by
 * `useInventoryListState` (values are local state synced from the URL), so a keystroke is never lost
 * to a slow navigation. Every control has a visible label; free text commits on blur/Enter.
 */
export function FilterBar({
  label,
  fields,
  values,
  onChange,
  onCommit,
  className,
}: {
  label: string;
  fields: readonly FilterField[];
  values: Record<string, string>;
  onChange: (name: string, value: string) => void;
  onCommit?: (name: string) => void;
  className?: string;
}) {
  return (
    <form
      role="search"
      aria-label={label}
      onSubmit={(event) => {
        event.preventDefault();
        fields.forEach((field) => onCommit?.(field.name));
      }}
      className={cn('flex flex-wrap items-end gap-3', className)}
    >
      {fields.map((field) => {
        const id = `filter-${field.name}`;
        if (field.type === 'checkbox') {
          return (
            <label key={field.name} htmlFor={id} className="flex items-center gap-2 pb-2 text-sm text-ink">
              <input
                id={id}
                type="checkbox"
                checked={values[field.name] === 'true'}
                onChange={(event) => onChange(field.name, event.target.checked ? 'true' : '')}
                className="h-4 w-4 accent-[var(--accent)]"
              />
              {field.label}
            </label>
          );
        }
        return (
          <div key={field.name} className="flex min-w-0 flex-col gap-1.5">
            <label htmlFor={id} className="text-xs font-semibold text-ink-muted">
              {field.label}
            </label>
            {field.type === 'select' ? (
              <select
                id={id}
                value={values[field.name] ?? ''}
                onChange={(event) => onChange(field.name, event.target.value)}
                className={cn(inputClass, 'w-auto max-w-full')}
              >
                {field.options.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            ) : (
              <input
                id={id}
                type={field.type}
                value={values[field.name] ?? ''}
                placeholder={field.type === 'text' ? field.placeholder : undefined}
                min={field.type === 'number' ? field.min : undefined}
                max={field.type === 'number' ? field.max : undefined}
                onChange={(event) => onChange(field.name, event.target.value)}
                onBlur={() => onCommit?.(field.name)}
                className={cn(inputClass, field.type === 'text' ? 'w-56 max-w-full' : 'w-auto max-w-full')}
              />
            )}
          </div>
        );
      })}
    </form>
  );
}
