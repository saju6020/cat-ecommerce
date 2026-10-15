'use client';

import { useEffect, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { STATUS_VARIANT } from '@/components/branches/BranchCalendarMonthNav';
import {
  STATUS_LABEL,
  formatDayLong,
  leadingBlankCount,
  type BranchCalendarDay,
} from '@/lib/format/branch-calendar';
import { cn } from '@/lib/utils';

/**
 * `components/branches/BranchMonthCalendar.tsx` — HO-ADM-002-FU1 §11.2, ADR-FE-171.
 *
 * A read-only month grid: a real `<table role="grid">` with a roving tabindex (one Tab stop) and the
 * WAI-ARIA APG grid keys. Shown at 768 px and wider; the page hides it below that and shows
 * `BranchCalendarDayList` instead. Each cell's accessible name is the full date plus the status
 * sentence; the visible content is a shortened, `aria-hidden` rendering of the same facts.
 */
const WEEKDAYS = [
  ['Monday', 'Mon'],
  ['Tuesday', 'Tue'],
  ['Wednesday', 'Wed'],
  ['Thursday', 'Thu'],
  ['Friday', 'Fri'],
  ['Saturday', 'Sat'],
  ['Sunday', 'Sun'],
] as const;

export interface BranchMonthCalendarProps {
  days: BranchCalendarDay[];
  month: string;
  monthHeadingId: string;
  /** PageUp / PageDown. `focusDay` is the focused day number, so a host that remounts the grid
   *  while the next month loads can hand it back as `restoreFocusDay`. */
  onMonthChange: (delta: -1 | 1, focusDay: number) => void;
  /** Move DOM focus to this day number (clamped to the month) once mounted. */
  restoreFocusDay?: number | null;
  onFocusRestored?: () => void;
}

function initialFocusDay(days: BranchCalendarDay[], restoreFocusDay?: number | null): number {
  if (restoreFocusDay) return restoreFocusDay;
  const today = days.findIndex((d) => d.isToday);
  return today >= 0 ? today + 1 : 1;
}

export function BranchMonthCalendar({
  days,
  month,
  monthHeadingId,
  onMonthChange,
  restoreFocusDay,
  onFocusRestored,
}: BranchMonthCalendarProps) {
  const total = days.length;
  const lead = leadingBlankCount(month);
  const [focusDay, setFocusDay] = useState(() => initialFocusDay(days, restoreFocusDay));
  const current = Math.min(Math.max(focusDay, 1), total);
  const tableRef = useRef<HTMLTableElement>(null);
  const restoreRef = useRef(Boolean(restoreFocusDay));

  const focusCell = (day: number) => {
    const date = days[day - 1]?.date;
    if (date) tableRef.current?.querySelector<HTMLElement>(`[data-date="${date}"]`)?.focus();
  };

  // After PageUp/PageDown the month changes (same instance, or a remount while the next month
  // loads): put DOM focus on the same day number, clamped to the new month's length.
  useEffect(() => {
    if (!restoreRef.current) return;
    restoreRef.current = false;
    focusCell(current);
    onFocusRestored?.();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- runs when the month changes only
  }, [month]);

  function move(day: number) {
    const next = Math.min(Math.max(day, 1), total);
    setFocusDay(next);
    focusCell(next);
  }

  function onKeyDown(event: React.KeyboardEvent, day: number) {
    const rowStart = day - ((lead + day - 1) % 7);
    switch (event.key) {
      case 'ArrowRight':
        return handled(event, () => move(day + 1));
      case 'ArrowLeft':
        return handled(event, () => move(day - 1));
      case 'ArrowDown':
        return handled(event, () => move(day + 7));
      case 'ArrowUp':
        return handled(event, () => move(day - 7));
      case 'Home':
        return handled(event, () => move(rowStart));
      case 'End':
        return handled(event, () => move(rowStart + 6));
      case 'PageUp':
      case 'PageDown':
        return handled(event, () => {
          restoreRef.current = true;
          setFocusDay(day);
          onMonthChange(event.key === 'PageUp' ? -1 : 1, day);
        });
    }
  }

  const cells: (BranchCalendarDay | null)[] = [
    ...Array<null>(lead).fill(null),
    ...days,
    ...Array<null>((7 - ((lead + total) % 7)) % 7).fill(null),
  ];
  const weeks = Array.from({ length: cells.length / 7 }, (_, i) => cells.slice(i * 7, i * 7 + 7));

  return (
    <table
      ref={tableRef}
      role="grid"
      aria-readonly="true"
      aria-labelledby={monthHeadingId}
      className="w-full table-fixed border-collapse"
    >
      <thead>
        <tr>
          {WEEKDAYS.map(([full, short]) => (
            <th
              key={full}
              scope="col"
              abbr={full}
              className="border border-border bg-surface-sunken px-2 py-1.5 text-left text-xs font-semibold text-ink-muted"
            >
              {short}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {weeks.map((week, weekIndex) => (
          <tr key={weekIndex}>
            {week.map((day, cellIndex) =>
              day ? (
                <DayCell
                  key={day.date}
                  day={day}
                  focused={days.indexOf(day) + 1 === current}
                  onFocus={() => setFocusDay(days.indexOf(day) + 1)}
                  onKeyDown={(event) => onKeyDown(event, days.indexOf(day) + 1)}
                />
              ) : (
                <td
                  key={`blank-${weekIndex}-${cellIndex}`}
                  role="gridcell"
                  aria-disabled="true"
                  className="border border-border bg-surface-sunken/50"
                />
              ),
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function handled(event: React.KeyboardEvent, action: () => void) {
  event.preventDefault();
  action();
}

function DayCell({
  day,
  focused,
  onFocus,
  onKeyDown,
}: {
  day: BranchCalendarDay;
  focused: boolean;
  onFocus: () => void;
  onKeyDown: (event: React.KeyboardEvent) => void;
}) {
  const dayNumber = Number(day.date.slice(8, 10));
  return (
    <td
      role="gridcell"
      data-date={day.date}
      tabIndex={focused ? 0 : -1}
      aria-current={day.isToday ? 'date' : undefined}
      onFocus={onFocus}
      onKeyDown={onKeyDown}
      className={cn(
        'h-28 min-w-0 border border-border p-1.5 align-top text-xs',
        day.isToday && 'bg-accent-soft/40',
      )}
    >
      <span className="sr-only">
        {day.isToday ? 'Today, ' : ''}
        {formatDayLong(day.date)}. {day.text}
      </span>
      <div aria-hidden="true" className="flex min-w-0 flex-col gap-1">
        <div className="flex items-center justify-between gap-1">
          <span className="text-sm font-semibold text-ink">{dayNumber}</span>
          {day.isToday && <span className="font-semibold text-accent-ink">Today</span>}
        </div>
        {day.intervals.map((interval) => (
          <span key={interval.start} className="text-ink">
            {interval.label}
          </span>
        ))}
        {day.closure && (
          <span className="break-words text-ink-muted">
            {day.status === 'reduced'
              ? `Closed ${day.closure.partialFrom}–${day.closure.partialTo}`
              : day.closure.reason}
          </span>
        )}
        <Badge
          variant={STATUS_VARIANT[day.status]}
          className="w-fit max-w-full whitespace-normal rounded-[var(--radius-sm)] px-1.5 py-0.5 text-[11px] leading-tight"
        >
          {STATUS_LABEL[day.status]}
        </Badge>
      </div>
    </td>
  );
}
