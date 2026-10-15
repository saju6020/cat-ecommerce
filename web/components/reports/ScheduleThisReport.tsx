import Link from 'next/link';
import { CalendarClock } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import type { ReportFilterValues } from '@/lib/hooks/useReportFilters';

/** `/staff/admin/report-schedules/new?reportKey=…&branchId=…&{parameters}` from the current filters. */
export function scheduleHref(reportKey: string, filters: ReportFilterValues): string {
  const params = new URLSearchParams({ reportKey });
  if (filters.branchId) params.set('branchId', filters.branchId);
  for (const [name, value] of Object.entries(filters.parameters)) {
    if (value !== '') params.set(name, value);
  }
  return `/staff/admin/report-schedules/new?${params.toString()}`;
}

/**
 * `components/reports/ScheduleThisReport.tsx` — HO-RPT-002 §11.3, US-RPT-06. Rendered by the viewer
 * only when the catalogue says `canSchedule`. A plain link: the schedule editor reads the report,
 * branch and parameters from the query string and the server previews the timing.
 */
export function ScheduleThisReport({
  reportKey,
  filters,
}: {
  reportKey: string;
  filters: ReportFilterValues;
}) {
  return (
    <Link
      href={scheduleHref(reportKey, filters)}
      className={buttonVariants({ variant: 'secondary' })}
    >
      <CalendarClock size={14} aria-hidden="true" />
      Schedule this report
    </Link>
  );
}
