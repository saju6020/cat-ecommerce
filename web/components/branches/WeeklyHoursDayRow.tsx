'use client';

import { Button } from '@/components/ui/button';
import { Plus, X } from 'lucide-react';
import { timeFieldLabel } from '@/lib/format/time';

export interface DayInterval {
  opens: string;
  closes: string;
}

/** `components/branches/WeeklyHoursDayRow.tsx` — one day of `WeeklyHoursEditor` (HO-ADM-002
 *  §11.2). 0–3 intervals; "Closed" in text when there are none; row-level errors shown inline so
 *  they land on the offending row rather than a form-level banner. */
export interface WeeklyHoursDayRowProps {
  day: string;
  timeZoneId: string;
  intervals: DayInterval[];
  errors: string[];
  onChange: (intervals: DayInterval[]) => void;
}

export function WeeklyHoursDayRow({
  day,
  timeZoneId,
  intervals,
  errors,
  onChange,
}: WeeklyHoursDayRowProps) {
  function updateInterval(index: number, field: 'opens' | 'closes', value: string) {
    const next = intervals.map((interval, i) =>
      i === index ? { ...interval, [field]: value } : interval,
    );
    onChange(next);
  }

  function addInterval() {
    onChange([...intervals, { opens: '09:00', closes: '17:00' }]);
  }

  function removeInterval(index: number) {
    onChange(intervals.filter((_, i) => i !== index));
  }

  return (
    <fieldset className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border p-3">
      <legend className="px-1 text-sm font-semibold text-ink">{day}</legend>

      {intervals.length === 0 && <p className="text-sm text-ink-muted">Closed</p>}

      {intervals.map((interval, index) => (
        <div key={index} className="flex flex-wrap items-end gap-2">
          <div className="flex flex-col gap-1">
            <label
              htmlFor={`${day}-opens-${index}`}
              className="text-xs font-semibold text-ink-muted"
            >
              {timeFieldLabel('Opens', timeZoneId)}
            </label>
            <input
              id={`${day}-opens-${index}`}
              type="time"
              value={interval.opens}
              onChange={(event) => updateInterval(index, 'opens', event.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label
              htmlFor={`${day}-closes-${index}`}
              className="text-xs font-semibold text-ink-muted"
            >
              {timeFieldLabel('Closes', timeZoneId)}
            </label>
            <input
              id={`${day}-closes-${index}`}
              type="time"
              value={interval.closes}
              onChange={(event) => updateInterval(index, 'closes', event.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={`Remove this interval on ${day}`}
            onClick={() => removeInterval(index)}
          >
            <X size={14} aria-hidden="true" />
          </Button>
        </div>
      ))}

      {errors.map((error, index) => (
        <p key={index} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      ))}

      {intervals.length < 3 && (
        <Button type="button" variant="secondary" size="sm" className="w-fit" onClick={addInterval}>
          <Plus size={12} aria-hidden="true" />
          Add interval
        </Button>
      )}
    </fieldset>
  );
}
