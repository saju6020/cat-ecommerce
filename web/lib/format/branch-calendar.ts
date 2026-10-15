import { addDays, endOfMonth, format, getISODay, parseISO, startOfMonth } from 'date-fns';
import {
  DAYS_OF_WEEK,
  type BranchCalendarInterval,
  type BranchClosure,
  type ClosureKind,
  type DayOfWeekName,
  type OpeningInterval,
} from '@/lib/api/admin-branches';
import { localDateOf, timeOnlyInZone, todayInZone } from '@/lib/format/time';

/**
 * `lib/format/branch-calendar.ts` — HO-ADM-002-FU1 §11.1, ADR-FE-170.
 *
 * The month model for the staff branch Calendar tab. **The server decides when the branch is
 * open**: a day is open only if `GET …/calendar` returned an interval that starts on it. The
 * closure list and the weekly hours are used here only to say *why* a day is closed or reduced;
 * they never turn a closed day into an open one, nor produce a time (HO-ADM-001 §20.7).
 *
 * Pure: no React, no I/O. Dates are `yyyy-MM-dd` strings; `date-fns` does the arithmetic on them
 * at noon so a browser zone with a DST change at local midnight cannot shift a day (§8.4).
 */

export type BranchDayStatus =
  'open' | 'reduced' | 'holiday' | 'closure' | 'weeklyClosed' | 'closed';

export interface BranchCalendarDay {
  /** `yyyy-MM-dd`, branch-local. */
  date: string;
  dayOfWeek: DayOfWeekName;
  status: BranchDayStatus;
  /** The server's open intervals that start on this date; `label` is `09:00–13:00` in branch time. */
  intervals: { start: string; end: string; label: string }[];
  closure: {
    kind: ClosureKind;
    reason: string;
    partialFrom: string | null;
    partialTo: string | null;
  } | null;
  isToday: boolean;
  isPast: boolean;
  /** The full status sentence (§11.1) — what a screen reader hears for the day. */
  text: string;
}

/** The words shown for each status (badge and legend). Never colour alone (UI-6). */
export const STATUS_LABEL: Record<BranchDayStatus, string> = {
  open: 'Open',
  reduced: 'Reduced hours',
  holiday: 'Closed — Public holiday',
  closure: 'Closed — closure',
  weeklyClosed: 'Closed (regular weekly closure)',
  closed: 'Closed',
};

const MONTH_PATTERN = /^(\d{4})-(0[1-9]|1[0-2])$/;
const DATE_FORMAT = 'yyyy-MM-dd';

function parseDate(date: string): Date {
  return parseISO(`${date}T12:00:00`);
}

function toDateString(date: Date): string {
  return format(date, DATE_FORMAT);
}

export function isValidMonth(raw: string | null | undefined): raw is string {
  if (!raw) return false;
  const match = MONTH_PATTERN.exec(raw);
  if (!match) return false;
  const year = Number(match[1]);
  return year >= 1900 && year <= 2200;
}

/** `'2027-02'` → `{ from: '2027-02-01', to: '2027-02-28' }`. */
export function monthBounds(month: string): { from: string; to: string } {
  const first = parseDate(`${month}-01`);
  return { from: toDateString(startOfMonth(first)), to: toDateString(endOfMonth(first)) };
}

/** A valid `yyyy-MM` is kept as-is; anything else (null, `2027-13`, junk) becomes the current
 *  month in the branch's zone (§8.3). */
export function parseMonthParam(raw: string | null, timeZoneId: string): string {
  return isValidMonth(raw) ? raw : todayInZone(timeZoneId).slice(0, 7);
}

export function shiftMonth(month: string, delta: number): string {
  const first = parseDate(`${month}-01`);
  return format(new Date(first.getFullYear(), first.getMonth() + delta, 1, 12), 'yyyy-MM');
}

/** `'2027-02'` → `'February 2027'`. */
export function formatMonthLabel(month: string): string {
  return format(parseDate(`${month}-01`), 'MMMM yyyy');
}

/** Empty cells before the 1st in a Monday-first week (1 Jan 2027 is a Friday → 4). */
export function leadingBlankCount(month: string): number {
  return getISODay(parseDate(`${month}-01`)) - 1;
}

/** `'2028-12-25'` → `'Mon 25 Dec'` (day list). */
export function formatDayShort(date: string): string {
  return format(parseDate(date), 'EEE d MMM');
}

/** `'2028-12-25'` → `'Monday 25 December 2028'` (accessible name). */
export function formatDayLong(date: string): string {
  return format(parseDate(date), 'EEEE d MMMM yyyy');
}

function weekdayName(date: string): DayOfWeekName {
  return DAYS_OF_WEEK[getISODay(parseDate(date)) - 1] as DayOfWeekName;
}

function classify(
  intervals: BranchCalendarDay['intervals'],
  onDate: BranchClosure[],
  weeklyClosed: boolean,
): Pick<BranchCalendarDay, 'status' | 'closure' | 'text'> {
  const partial = onDate.find((c) => c.partialFrom);
  const full = (kind: ClosureKind) => onDate.find((c) => !c.partialFrom && c.kind === kind);
  const asClosure = (c: BranchClosure) => ({
    kind: c.kind,
    reason: c.reason,
    partialFrom: c.partialFrom,
    partialTo: c.partialTo,
  });
  const labels = intervals.map((i) => i.label).join(', ');

  if (intervals.length > 0 && partial) {
    return {
      status: 'reduced',
      closure: asClosure(partial),
      text: `Reduced hours: ${labels}. Closed ${partial.partialFrom}–${partial.partialTo}: ${partial.reason}`,
    };
  }
  // A full-day closure never hides server intervals (ADR-FE-170): row 2 precedes rows 3–4.
  if (intervals.length > 0) {
    return { status: 'open', closure: null, text: `Open ${labels}` };
  }
  const holiday = full('PublicHoliday');
  if (holiday) {
    return {
      status: 'holiday',
      closure: asClosure(holiday),
      text: `Closed — Public holiday: ${holiday.reason}`,
    };
  }
  const closure = full('Closure');
  if (closure) {
    return { status: 'closure', closure: asClosure(closure), text: `Closed — ${closure.reason}` };
  }
  if (partial) {
    return {
      status: 'closure',
      closure: asClosure(partial),
      text: `Closed — ${partial.reason} (part-day closure ${partial.partialFrom}–${partial.partialTo} covers all opening hours)`,
    };
  }
  if (weeklyClosed) {
    return { status: 'weeklyClosed', closure: null, text: 'Closed (regular weekly closure)' };
  }
  return { status: 'closed', closure: null, text: 'Closed' };
}

export function buildBranchCalendarMonth(input: {
  month: string;
  timeZoneId: string;
  weeklyHours: OpeningInterval[];
  intervals: BranchCalendarInterval[];
  /** `undefined` while loading or failed: no reason can be given, state is unaffected. */
  closures: BranchClosure[] | undefined;
  today: string;
}): BranchCalendarDay[] {
  const { month, timeZoneId, weeklyHours, intervals, closures, today } = input;
  const { from, to } = monthBounds(month);
  const active = (closures ?? []).filter((c) => !c.isCancelled);

  const byDate = new Map<string, BranchCalendarDay['intervals']>();
  for (const interval of [...intervals].sort((a, b) => a.start.localeCompare(b.start))) {
    const date = localDateOf(interval.start, timeZoneId);
    const label = `${timeOnlyInZone(interval.start, timeZoneId)}–${timeOnlyInZone(interval.end, timeZoneId)}`;
    byDate.set(date, [...(byDate.get(date) ?? []), { ...interval, label }]);
  }

  const days: BranchCalendarDay[] = [];
  for (let cursor = parseDate(from); toDateString(cursor) <= to; cursor = addDays(cursor, 1)) {
    const date = toDateString(cursor);
    const dayOfWeek = weekdayName(date);
    const dayIntervals = byDate.get(date) ?? [];
    const onDate = active.filter((c) => c.startsOn <= date && date <= c.endsOn);
    const weeklyClosed = !weeklyHours.some((h) => h.dayOfWeek === dayOfWeek);
    days.push({
      date,
      dayOfWeek,
      intervals: dayIntervals,
      isToday: date === today,
      isPast: date < today,
      ...classify(dayIntervals, onDate, weeklyClosed),
    });
  }
  return days;
}
