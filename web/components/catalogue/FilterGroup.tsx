'use client';

import * as React from 'react';
import { ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';

/**
 * A real `<fieldset>`/`<legend>` group (UI-6): the legend names the group for assistive tech.
 * 2026-10-16 (owner: tidy the shop filters): the legend is a disclosure button. A group starts open
 * when `defaultOpen` or when it already holds a choice (`active`), so a selection never hides;
 * a closed group keeps its fieldset and name, its options are `hidden`.
 */
export function FilterGroup({
  legend,
  defaultOpen = true,
  active = false,
  children,
}: {
  legend: string;
  defaultOpen?: boolean;
  active?: boolean;
  children: React.ReactNode;
}) {
  const [open, setOpen] = React.useState(defaultOpen || active);
  const bodyId = React.useId();
  // A choice made elsewhere (a chip, the URL) opens the group it belongs to.
  React.useEffect(() => {
    if (active) setOpen(true);
  }, [active]);
  return (
    <fieldset className="m-0 min-w-0 border-0 border-b border-border p-0 pb-3 last:border-b-0 last:pb-0">
      <legend className="w-full p-0">
        <button
          type="button"
          aria-expanded={open}
          aria-controls={bodyId}
          onClick={() => setOpen((current) => !current)}
          className="-mx-2 flex h-9 w-[calc(100%+1rem)] items-center justify-between gap-2 rounded-[10px] px-2 text-left text-sm font-bold text-ink hover:bg-surface-sunken"
        >
          <span className="flex items-center gap-2">
            {legend}
            {active && !open && (
              <span aria-hidden="true" className="size-1.5 rounded-full bg-accent" />
            )}
          </span>
          <ChevronDown
            size={16}
            aria-hidden="true"
            className={cn(
              'flex-none text-ink-muted transition-transform duration-200 motion-reduce:transition-none',
              !open && '-rotate-90',
            )}
          />
        </button>
      </legend>
      <div id={bodyId} hidden={!open} className="mt-1 flex flex-col gap-0.5">
        {children}
      </div>
    </fieldset>
  );
}

/** The option count, as a small muted badge (was "(595)"). */
function Count({ value }: { value: number }) {
  return (
    <span className="min-w-7 rounded-pill bg-surface-sunken px-2 py-0.5 text-center text-[11px] font-semibold tabular-nums text-ink-muted">
      {/* Read as "(595)", as before; only the brackets are hidden on screen. */}
      <span className="sr-only">(</span>
      {value}
      <span className="sr-only">)</span>
    </span>
  );
}

const optionRow =
  'flex min-h-9 cursor-pointer items-center gap-2.5 rounded-[10px] px-2 -mx-2 text-sm text-ink hover:bg-surface-sunken';

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
    <label htmlFor={id} className={cn(optionRow, checked && 'font-semibold')}>
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="h-4 w-4 flex-none accent-[var(--accent)]"
      />
      {icon}
      <span className="min-w-0 flex-1 py-1.5 leading-snug [overflow-wrap:anywhere]">{label}</span>
      {count !== undefined && <Count value={count} />}
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
    <label
      htmlFor={id}
      className={cn(
        optionRow,
        checked && 'bg-accent-soft font-semibold text-accent-ink hover:bg-accent-soft',
      )}
    >
      <input
        id={id}
        type="radio"
        name={name}
        checked={checked}
        onChange={onSelect}
        className="h-4 w-4 flex-none accent-[var(--accent)]"
      />
      <span className="min-w-0 flex-1 py-1.5 leading-snug [overflow-wrap:anywhere]">{label}</span>
      {count !== undefined && <Count value={count} />}
    </label>
  );
}
