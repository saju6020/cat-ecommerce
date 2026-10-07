import * as React from 'react';

/** A real `<fieldset>`/`<legend>` group (UI-6): the legend names the group for assistive tech. */
export function FilterGroup({ legend, children }: { legend: string; children: React.ReactNode }) {
  return (
    <fieldset className="m-0 min-w-0 border-0 border-b border-border p-0 pb-4">
      <legend className="mb-2 p-0 text-sm font-bold text-ink">{legend}</legend>
      <div className="flex flex-col gap-1.5">{children}</div>
    </fieldset>
  );
}

export function CheckOption({
  id,
  label,
  count,
  checked,
  onChange,
  icon,
}: {
  id: string;
  label: string;
  count?: number;
  checked: boolean;
  onChange: (checked: boolean) => void;
  icon?: React.ReactNode;
}) {
  return (
    <label htmlFor={id} className="flex min-h-8 cursor-pointer items-center gap-2 text-sm text-ink">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="h-4 w-4 accent-[var(--accent)]"
      />
      {icon}
      <span className="min-w-0 flex-1">{label}</span>
      {count !== undefined && <span className="text-xs text-ink-muted">({count})</span>}
    </label>
  );
}

export function RadioOption({
  id,
  name,
  label,
  count,
  checked,
  onSelect,
}: {
  id: string;
  name: string;
  label: string;
  count?: number;
  checked: boolean;
  onSelect: () => void;
}) {
  return (
    <label htmlFor={id} className="flex min-h-8 cursor-pointer items-center gap-2 text-sm text-ink">
      <input
        id={id}
        type="radio"
        name={name}
        checked={checked}
        onChange={onSelect}
        className="h-4 w-4 accent-[var(--accent)]"
      />
      <span className="min-w-0 flex-1">{label}</span>
      {count !== undefined && <span className="text-xs text-ink-muted">({count})</span>}
    </label>
  );
}
