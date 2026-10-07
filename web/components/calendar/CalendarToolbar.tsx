'use client';

import Link from 'next/link';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { CalendarView } from '@/lib/api/staff-appointments';

export type CalendarRange = 'day' | 'week';

function Segment<T extends string>({
  label,
  value,
  options,
  onChange,
}: {
  label: string;
  value: T;
  options: Array<{ value: T; label: string }>;
  onChange: (value: T) => void;
}) {
  return (
    <div
      role="group"
      aria-label={label}
      className="inline-flex overflow-hidden rounded-[var(--radius-sm)] border border-border-strong"
    >
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={value === option.value}
          onClick={() => onChange(option.value)}
          className={cn(
            'px-3 py-1.5 text-sm font-semibold focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent',
            value === option.value
              ? 'bg-accent text-white'
              : 'bg-surface text-ink hover:bg-surface-sunken',
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

/**
 * `components/calendar/CalendarToolbar.tsx` — HO-APT-002 §11.6: Day / Week, By vet / By room, a
 * resource multi-select, previous / today / next, the date label (branch zone) and the
 * **Flagged (n)** link. Toggle buttons carry `aria-pressed`; the resource list is a real group of
 * checkboxes behind a disclosure, so it works on a keyboard and a phone.
 */
export function CalendarToolbar({
  range,
  onRangeChange,
  view,
  onViewChange,
  resources,
  selectedIds,
  onSelectedIdsChange,
  dateLabel,
  zone,
  onPrevious,
  onToday,
  onNext,
  flaggedCount,
}: {
  range: CalendarRange;
  onRangeChange: (range: CalendarRange) => void;
  view: CalendarView;
  onViewChange: (view: CalendarView) => void;
  resources: Array<{ id: string; name: string }>;
  /** Empty means "all". */
  selectedIds: string[];
  onSelectedIdsChange: (ids: string[]) => void;
  dateLabel: string;
  zone: string;
  onPrevious: () => void;
  onToday: () => void;
  onNext: () => void;
  flaggedCount: number | null;
}) {
  const noun = view === 'performer' ? 'vets' : 'rooms';
  const toggle = (id: string, checked: boolean) => {
    const base = selectedIds.length === 0 ? resources.map((r) => r.id) : selectedIds;
    const next = checked ? [...new Set([...base, id])] : base.filter((x) => x !== id);
    onSelectedIdsChange(next.length === resources.length ? [] : next);
  };
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <Segment
          label="Range"
          value={range}
          onChange={onRangeChange}
          options={[
            { value: 'day', label: 'Day' },
            { value: 'week', label: 'Week' },
          ]}
        />
        <Segment
          label="Group by"
          value={view}
          onChange={onViewChange}
          options={[
            { value: 'performer', label: 'By vet' },
            { value: 'room', label: 'By room' },
          ]}
        />
        <details className="relative">
          <summary className="cursor-pointer rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-1.5 text-sm font-semibold text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
            Show{' '}
            {selectedIds.length === 0
              ? `all ${noun}`
              : `${selectedIds.length} of ${resources.length} ${noun}`}
          </summary>
          <fieldset className="absolute z-20 mt-1 flex min-w-48 flex-col gap-1 rounded-[var(--radius-md)] border border-border bg-surface p-3 shadow-modal">
            <legend className="sr-only">Choose which {noun} to show</legend>
            {resources.map((resource) => (
              <label key={resource.id} className="flex items-center gap-2 text-sm text-ink">
                <input
                  type="checkbox"
                  checked={selectedIds.length === 0 || selectedIds.includes(resource.id)}
                  onChange={(event) => toggle(resource.id, event.target.checked)}
                />
                {resource.name}
              </label>
            ))}
          </fieldset>
        </details>
        <Link
          href="/staff/appointments/flagged"
          className="ml-auto text-sm font-semibold text-accent-ink underline underline-offset-2"
        >
          Flagged{flaggedCount !== null ? ` (${flaggedCount})` : ''}
        </Link>
        <Link
          href="/staff/appointments/new"
          className="text-sm font-semibold text-accent-ink underline underline-offset-2"
        >
          New appointment
        </Link>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="secondary"
          size="icon"
          aria-label={`Previous ${range}`}
          onClick={onPrevious}
        >
          <ChevronLeft size={16} aria-hidden="true" />
        </Button>
        <Button variant="secondary" size="sm" onClick={onToday}>
          Today
        </Button>
        <Button variant="secondary" size="icon" aria-label={`Next ${range}`} onClick={onNext}>
          <ChevronRight size={16} aria-hidden="true" />
        </Button>
        <h2 className="ml-2 text-[15px] font-semibold text-ink" aria-live="polite">
          {dateLabel} <span className="font-normal text-ink-muted">({zone})</span>
        </h2>
      </div>
    </div>
  );
}
