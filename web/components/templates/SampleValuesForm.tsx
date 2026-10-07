'use client';

import type { NotificationTemplateVariable } from '@/lib/api/admin-notification-templates';

/**
 * `components/templates/SampleValuesForm.tsx` — HO-NTF-002 §11.3. A small form under the preview:
 * one input per placeholder, pre-filled with the definition's sample. Edits are sent with the
 * preview request as `sampleValues` and are **never saved** — they are not part of the template
 * form, so they cannot make it dirty or reach `PUT`.
 */
export function SampleValuesForm({
  variables,
  values,
  onChange,
}: {
  variables: NotificationTemplateVariable[];
  values: Record<string, string>;
  onChange: (name: string, value: string) => void;
}) {
  if (variables.length === 0) return null;
  return (
    <details className="rounded-[var(--radius-md)] border border-border bg-surface p-3">
      <summary className="cursor-pointer text-sm font-semibold text-ink">
        Sample values for the preview
      </summary>
      <p className="mt-2 text-xs text-ink-muted">
        Used only to fill the preview. They are not saved with the template.
      </p>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        {variables.map((variable) => {
          const id = `sample-${variable.name}`;
          return (
            <div key={variable.name} className="flex flex-col gap-1">
              <label htmlFor={id} className="text-xs font-semibold text-ink-muted">
                {variable.name}
              </label>
              <input
                id={id}
                type="text"
                value={values[variable.name] ?? variable.sample}
                onChange={(event) => onChange(variable.name, event.target.value)}
                className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink"
              />
            </div>
          );
        })}
      </div>
    </details>
  );
}
