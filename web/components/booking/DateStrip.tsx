'use client';

import { useState } from 'react';
import { cn } from '@/lib/utils';
import { dateRange, dateStripParts, formatBranchDateLong } from '@/lib/format/appointment';
import { MonthGrid } from './MonthGrid';

export const DATE_STRIP_DAYS = 14;

function weekdayIndex(dateIso: string): number {
  const [y, m, d] = dateIso.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(y, m - 1, d)).getUTCDay();
}

/**
 * `components/booking/DateStrip.tsx` — HO-APT-002 §11.1 SlotStep, OQ-1: a 14-day strip plus an
 * in-repo month grid ("Pick a date…"). Dates are branch-local `yyyy-MM-dd` strings; `todayIso` is
 * today in the BRANCH zone, passed in, so a phone in another zone never shifts the strip. A day
 * the clinic's published weekly hours show as closed is labelled "Closed" — display of opening
 * hours only; whether a day has times is always the server's answer.
 */
export function DateStrip({
  todayIso,
  selectedDate,
  onSelect,
  openWeekdays,
}: {
  todayIso: string;
  selectedDate: string;
  onSelect: (dateIso: string) => void;
  openWeekdays?: number[];
}) {
  const [monthOpen, setMonthOpen] = useState(false);
  const days = dateRange(todayIso, DATE_STRIP_DAYS);
  const inStrip = days.includes(selectedDate);

  return (
    <div className="flex flex-col gap-3">
      <span id="date-strip-label" className="text-sm font-semibold text-ink">
        Date
      </span>
      <div
        role="group"
        aria-labelledby="date-strip-label"
        className="grid grid-cols-7 gap-1.5 sm:grid-cols-[repeat(14,minmax(0,1fr))]"
      >
        {days.map((dateIso) => {
          const parts = dateStripParts(dateIso);
          const closed = openWeekdays ? !openWeekdays.includes(weekdayIndex(dateIso)) : false;
          const selected = dateIso === selectedDate;
          return (
            <button
              key={dateIso}
              type="button"
              aria-pressed={selected}
              aria-label={`${formatBranchDateLong(dateIso)}${closed ? ' — clinic closed' : ''}`}
              onClick={() => onSelect(dateIso)}
              className={cn(
                'flex min-w-0 flex-col items-center rounded-[var(--radius-sm)] border px-0.5 py-1.5 text-xs leading-tight focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                selected
                  ? 'border-accent bg-accent text-white'
                  : closed
                    ? 'border-border bg-surface-sunken text-ink-muted'
                    : 'border-border bg-surface text-ink hover:bg-surface-sunken',
              )}
            >
              <span>{parts.weekday}</span>
              <span className="text-[15px] font-bold">{parts.day}</span>
              <span>{closed ? 'Closed' : parts.month}</span>
            </button>
          );
        })}
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          aria-expanded={monthOpen}
          aria-controls="month-grid"
          onClick={() => setMonthOpen((open) => !open)}
          className="rounded-[var(--radius-sm)] border border-dashed border-border-strong bg-surface px-3 py-1.5 text-sm font-semibold text-accent-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          Pick a date…
        </button>
        {!inStrip && (
          <span className="text-sm text-ink-muted">
            Chosen: <strong className="text-ink">{formatBranchDateLong(selectedDate)}</strong>
          </span>
        )}
      </div>
      {monthOpen && (
        <MonthGrid
          id="month-grid"
          todayIso={todayIso}
          selectedDate={selectedDate}
          initialMonth={selectedDate.slice(0, 7)}
          openWeekdays={openWeekdays}
          onSelect={(dateIso) => {
            onSelect(dateIso);
            setMonthOpen(false);
          }}
        />
      )}
    </div>
  );
}
