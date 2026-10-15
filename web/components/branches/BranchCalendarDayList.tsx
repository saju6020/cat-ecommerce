import { Badge } from '@/components/ui/badge';
import { STATUS_LABEL, formatDayShort, type BranchCalendarDay } from '@/lib/format/branch-calendar';
import { STATUS_VARIANT } from '@/components/branches/BranchCalendarMonthNav';

/** `components/branches/BranchCalendarDayList.tsx` — HO-ADM-002-FU1 §11.2. The month as a day list
 *  below 768 px: no grid semantics, no sideways scroll, long reasons wrap (UI-1, ADR-FE-171). Closure
 *  reasons are rendered as text only. */
export function BranchCalendarDayList({
  days,
  monthHeadingId,
}: {
  days: BranchCalendarDay[];
  monthHeadingId: string;
}) {
  return (
    <ol aria-labelledby={monthHeadingId} className="m-0 flex list-none flex-col p-0">
      {days.map((day) => (
        <li
          key={day.date}
          className="flex flex-col gap-1 border-b border-border py-2.5 first:border-t"
        >
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold text-ink">{formatDayShort(day.date)}</span>
            {day.isToday && <span className="text-xs font-semibold text-accent-ink">Today</span>}
            <Badge variant={STATUS_VARIANT[day.status]} className="whitespace-normal">
              {STATUS_LABEL[day.status]}
            </Badge>
          </div>
          <p className="m-0 break-words text-sm text-ink-muted">{day.text}</p>
        </li>
      ))}
    </ol>
  );
}
