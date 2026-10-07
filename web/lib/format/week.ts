import { startOfWeek, addDays, format, parseISO } from 'date-fns';

/**
 * `lib/format/week.ts` — HO-ADM-002 §11.4, OQ-1 (roster view defaults to this week). Pure
 * `yyyy-MM-dd` calendar arithmetic with `date-fns` (§10 — allowed for date arithmetic on
 * `yyyy-MM-dd` strings, never for branch wall-clock times); no scheduler/calendar library.
 */
export function getWeekDates(anchorIso: string): string[] {
  const monday = startOfWeek(parseISO(anchorIso), { weekStartsOn: 1 });
  return Array.from({ length: 7 }, (_, i) => format(addDays(monday, i), 'yyyy-MM-dd'));
}

export function shiftWeek(anchorIso: string, weeks: number): string {
  return format(addDays(parseISO(anchorIso), weeks * 7), 'yyyy-MM-dd');
}

export function todayIso(): string {
  return format(new Date(), 'yyyy-MM-dd');
}
