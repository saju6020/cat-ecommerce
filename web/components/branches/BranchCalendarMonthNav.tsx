import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { formatMonthLabel, shiftMonth, STATUS_LABEL } from '@/lib/format/branch-calendar';
import type { BranchDayStatus } from '@/lib/format/branch-calendar';

/** `components/branches/BranchCalendarMonthNav.tsx` — HO-ADM-002-FU1 §11.2. The month heading, the
 *  previous / next / this-month links (the month is the `?month=` URL param, ADR-FE-171), the time
 *  zone caption and the legend. Presentational only. */
export interface BranchCalendarMonthNavProps {
  month: string;
  /** The month the branch is in right now; the "This month" link is hidden when already on it. */
  currentMonth: string;
  timeZoneId: string;
  /** `/staff/admin/branches/{id}/calendar` */
  basePath: string;
  headingId: string;
  /** True when the month contains days before today in the branch's zone (F9). */
  hasPastDays: boolean;
}

/** The five states a day can show (the plain "Closed" fallback needs no legend entry). */
const LEGEND: { status: BranchDayStatus; meaning: string; variant: StatusVariant }[] = [
  { status: 'open', meaning: 'Open with the times shown', variant: 'success' },
  { status: 'reduced', meaning: 'A part-day closure removed some hours', variant: 'warning' },
  { status: 'holiday', meaning: 'Closed for a public holiday', variant: 'danger' },
  { status: 'closure', meaning: 'Closed for a closure', variant: 'danger' },
  { status: 'weeklyClosed', meaning: 'No opening hours on that weekday', variant: 'neutral' },
];

type StatusVariant = 'success' | 'warning' | 'danger' | 'neutral';

export const STATUS_VARIANT: Record<BranchDayStatus, StatusVariant> = {
  open: 'success',
  reduced: 'warning',
  holiday: 'danger',
  closure: 'danger',
  weeklyClosed: 'neutral',
  closed: 'neutral',
};

const NAV_LINK =
  'rounded-[var(--radius-sm)] border border-border px-3 py-1.5 text-sm font-semibold text-ink no-underline hover:bg-surface-sunken';

export function BranchCalendarMonthNav({
  month,
  currentMonth,
  timeZoneId,
  basePath,
  headingId,
  hasPastDays,
}: BranchCalendarMonthNavProps) {
  const href = (target: string) => `${basePath}?month=${target}`;
  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <h3 id={headingId} className="font-display text-base font-semibold text-ink">
          {formatMonthLabel(month)}
        </h3>
        <nav aria-label="Calendar month" className="flex flex-wrap gap-2">
          <Link href={href(shiftMonth(month, -1))} className={NAV_LINK}>
            Previous month
          </Link>
          <Link href={href(shiftMonth(month, 1))} className={NAV_LINK}>
            Next month
          </Link>
          {month !== currentMonth && (
            <Link href={href(currentMonth)} className={NAV_LINK}>
              This month
            </Link>
          )}
        </nav>
      </div>
      <p className="text-sm text-ink-muted">
        Times in {timeZoneId}.
        {hasPastDays &&
          ' Past days are shown with the current opening hours; earlier hours are not kept.'}
      </p>
      <dl aria-label="Legend" className="flex flex-wrap gap-x-4 gap-y-2 text-sm text-ink-muted">
        {LEGEND.map((entry) => (
          <div key={entry.status} className="flex items-center gap-2">
            <dt>
              <Badge variant={entry.variant} className="whitespace-normal">
                {STATUS_LABEL[entry.status]}
              </Badge>
            </dt>
            <dd>{entry.meaning}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
