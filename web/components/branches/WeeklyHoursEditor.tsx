'use client';

import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { describeApiError } from '@/lib/api/errors';
import { DAYS_OF_WEEK, type DayOfWeekName, type OpeningInterval } from '@/lib/api/admin-branches';
import { WeeklyHoursDayRow, type DayInterval } from './WeeklyHoursDayRow';

type WeekState = Record<DayOfWeekName, DayInterval[]>;

/** `noUncheckedIndexedAccess` makes every `WeekState[day]` read `DayInterval[] | undefined` even
 *  though `toWeekState` always populates all seven keys — one small helper rather than a
 *  non-null assertion at every call site. */
function dayOf(state: WeekState, day: DayOfWeekName): DayInterval[] {
  return state[day] ?? [];
}

function toWeekState(intervals: OpeningInterval[]): WeekState {
  const state = Object.fromEntries(
    DAYS_OF_WEEK.map((day) => [day, [] as DayInterval[]]),
  ) as WeekState;
  for (const interval of intervals) {
    state[interval.dayOfWeek] = [
      ...dayOf(state, interval.dayOfWeek),
      { opens: interval.opens, closes: interval.closes },
    ];
  }
  for (const day of DAYS_OF_WEEK) {
    state[day] = [...dayOf(state, day)].sort((a, b) => a.opens.localeCompare(b.opens));
  }
  return state;
}

function toIntervals(state: WeekState): OpeningInterval[] {
  return DAYS_OF_WEEK.flatMap((day) =>
    dayOf(state, day).map((interval) => ({ dayOfWeek: day, ...interval })),
  );
}

/** Mirrors HO-ADM-001 §8.2's invariants client-side, so errors land on the offending row before
 *  submit rather than only after the server refuses the whole week (§11.2, U-B1). */
function validateDay(intervals: DayInterval[]): string[] {
  const errors: string[] = [];
  if (intervals.length > 3) errors.push('At most 3 intervals are allowed in one day.');
  for (const interval of intervals) {
    if (!interval.opens || !interval.closes) continue;
    if (interval.opens >= interval.closes) {
      errors.push(
        `${interval.opens}–${interval.closes} is not a valid interval (opens must be before closes; no interval may cross midnight).`,
      );
    }
  }
  const sorted = [...intervals].sort((a, b) => a.opens.localeCompare(b.opens));
  for (let i = 1; i < sorted.length; i += 1) {
    const previous = sorted[i - 1];
    const current = sorted[i];
    if (!previous || !current) continue;
    if (current.opens < previous.closes) {
      errors.push(
        `${previous.opens}–${previous.closes} overlaps ${current.opens}–${current.closes}.`,
      );
    }
  }
  return errors;
}

/** `components/branches/WeeklyHoursEditor.tsx` — ⭐ HO-ADM-002 §11.2, US-ADM-01. Seven day rows,
 *  each with 0–3 intervals; "Copy Monday to weekdays"; the whole week is replaced in one save
 *  (`SetWeeklyHoursCommand`). */
export interface WeeklyHoursEditorProps {
  timeZoneId: string;
  weeklyHours: OpeningInterval[];
  onSave: (intervals: OpeningInterval[]) => Promise<unknown>;
  isPending: boolean;
}

export function WeeklyHoursEditor({
  timeZoneId,
  weeklyHours,
  onSave,
  isPending,
}: WeeklyHoursEditorProps) {
  const initial = useMemo(() => toWeekState(weeklyHours), [weeklyHours]);
  const [week, setWeek] = useState<WeekState>(initial);
  const [saved, setSaved] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);

  const isDirty = JSON.stringify(week) !== JSON.stringify(initial);
  const dayErrors = Object.fromEntries(
    DAYS_OF_WEEK.map((day) => [day, validateDay(dayOf(week, day))]),
  ) as Record<DayOfWeekName, string[]>;
  const hasErrors = Object.values(dayErrors).some((errors) => errors.length > 0);

  function copyMondayToWeekdays() {
    const monday = dayOf(week, 'Monday').map((interval) => ({ ...interval }));
    setWeek((current) => ({
      ...current,
      Tuesday: monday.map((i) => ({ ...i })),
      Wednesday: monday.map((i) => ({ ...i })),
      Thursday: monday.map((i) => ({ ...i })),
      Friday: monday.map((i) => ({ ...i })),
    }));
    setSaved(false);
  }

  async function handleSave() {
    setServerError(null);
    setSaved(false);
    try {
      await onSave(toIntervals(week));
      setSaved(true);
    } catch (error) {
      setServerError(describeApiError(error));
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <UnsavedChangesGuard when={isDirty && !saved} />

      {serverError && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {serverError}
        </p>
      )}
      {saved && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Opening hours saved.
        </p>
      )}

      <Button
        type="button"
        variant="secondary"
        size="sm"
        className="w-fit"
        onClick={copyMondayToWeekdays}
      >
        Copy Monday to weekdays
      </Button>

      <div className="flex flex-col gap-3">
        {DAYS_OF_WEEK.map((day) => (
          <WeeklyHoursDayRow
            key={day}
            day={day}
            timeZoneId={timeZoneId}
            intervals={dayOf(week, day)}
            errors={dayErrors[day] ?? []}
            onChange={(intervals) => {
              setWeek((current) => ({ ...current, [day]: intervals }));
              setSaved(false);
            }}
          />
        ))}
      </div>

      <Button
        type="button"
        onClick={handleSave}
        disabled={isPending || hasErrors}
        aria-busy={isPending}
        className="w-fit"
      >
        {isPending ? 'Saving…' : 'Save opening hours'}
      </Button>
    </div>
  );
}
