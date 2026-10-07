'use client';

import { useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import { addDaysIso, formatBranchDateLong } from '@/lib/format/appointment';

function monthStart(month: string): string {
  return `${month}-01`;
}

function shiftMonth(month: string, delta: number): string {
  const [y, m] = month.split('-').map(Number) as [number, number];
  const next = new Date(Date.UTC(y, m - 1 + delta, 1));
  return next.toISOString().slice(0, 7);
}

function monthLabel(month: string): string {
  const [y, m] = month.split('-').map(Number) as [number, number];
  return new Intl.DateTimeFormat('en-GB', {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(y, m - 1, 1)));
}

const WEEKDAY_HEADINGS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

/**
 * `components/booking/MonthGrid.tsx` — HO-APT-002 OQ-1, §10: the in-repo month picker (no
 * date-picker library). A grid of real buttons; days before today are disabled. Weeks start on
 * Monday. Pure calendar arithmetic on `yyyy-MM-dd` strings — no browser zone involved.
 */
export function MonthGrid({
  id,
  todayIso,
  selectedDate,
  initialMonth,
  openWeekdays,
  onSelect,
}: {
  id?: string;
  todayIso: string;
  selectedDate: string;
  initialMonth: string;
  openWeekdays?: number[];
  onSelect: (dateIso: string) => void;
}) {
  const [month, setMonth] = useState(initialMonth);
  const first = monthStart(month);
  const [y, m, d] = first.split('-').map(Number) as [number, number, number];
  const leading = (new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7;
  const daysInMonth = new Date(Date.UTC(y, m, 0)).getUTCDate();
  const cells: Array<string | null> = [
    ...Array.from({ length: leading }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => addDaysIso(first, i)),
  ];

  return (
    <div
      id={id}
      className="w-full max-w-sm rounded-[var(--radius-md)] border border-border bg-surface p-3"
    >
      <div className="mb-2 flex items-center justify-between">
        <button
          type="button"
          aria-label="Previous month"
          onClick={() => setMonth(shiftMonth(month, -1))}
          disabled={shiftMonth(month, 0) <= todayIso.slice(0, 7)}
          className="rounded-[var(--radius-sm)] p-1.5 text-ink hover:bg-surface-sunken disabled:opacity-40 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <ChevronLeft size={16} aria-hidden="true" />
        </button>
        <p aria-live="polite" className="text-sm font-semibold text-ink">
          {monthLabel(month)}
        </p>
        <button
          type="button"
          aria-label="Next month"
          onClick={() => setMonth(shiftMonth(month, 1))}
          className="rounded-[var(--radius-sm)] p-1.5 text-ink hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <ChevronRight size={16} aria-hidden="true" />
        </button>
      </div>
      <div className="grid grid-cols-7 gap-1 text-center text-[11px] font-semibold uppercase text-ink-muted">
        {WEEKDAY_HEADINGS.map((heading) => (
          <span key={heading}>{heading}</span>
        ))}
      </div>
      <div className="mt-1 grid grid-cols-7 gap-1">
        {cells.map((dateIso, index) => {
          if (!dateIso) return <span key={`blank-${index}`} />;
          const past = dateIso < todayIso;
          const weekday = (new Date(`${dateIso}T00:00:00Z`).getUTCDay() + 0) % 7;
          const closed = openWeekdays ? !openWeekdays.includes(weekday) : false;
          return (
            <button
              key={dateIso}
              type="button"
              disabled={past}
              aria-pressed={dateIso === selectedDate}
              aria-label={`${formatBranchDateLong(dateIso)}${closed ? ' — clinic closed' : ''}`}
              onClick={() => onSelect(dateIso)}
              className={cn(
                'rounded-[var(--radius-sm)] py-1.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-35',
                dateIso === selectedDate
                  ? 'bg-accent font-bold text-white'
                  : closed
                    ? 'bg-surface-sunken text-ink-muted'
                    : 'text-ink hover:bg-surface-sunken',
              )}
            >
              {Number(dateIso.slice(8))}
            </button>
          );
        })}
      </div>
    </div>
  );
}
