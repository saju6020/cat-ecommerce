'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { SimplePager } from '@/components/notifications/SimplePager';
import { LoadingState } from '@/components/common/LoadingState';
import { ReportErrorView } from '@/components/reports/ReportErrorView';
import { ScheduleTable } from '@/components/report-schedules/ScheduleTable';
import { buttonVariants } from '@/components/ui/button';
import { SCHEDULE_PAGE_SIZE, SCHEDULE_STATUSES } from '@/lib/api/report-schedules';
import { useReportCatalogue } from '@/lib/hooks/useReports';
import { useScheduleList } from '@/lib/hooks/useReportSchedules';

const SELECT =
  'w-full max-w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink';

function ScheduleListContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const status = searchParams.get('status') ?? '';
  const reportKey = searchParams.get('reportKey') ?? '';
  const pageNumber = Math.max(1, Number(searchParams.get('page') ?? '1') || 1);

  const catalogue = useReportCatalogue();
  const list = useScheduleList({ status, reportKey, pageNumber, pageSize: SCHEDULE_PAGE_SIZE });

  function write(next: Record<string, string>) {
    const params = new URLSearchParams(searchParams.toString());
    for (const [name, value] of Object.entries(next)) {
      if (value) params.set(name, value);
      else params.delete(name);
    }
    const query = params.toString();
    router.push(query ? `${pathname}?${query}` : pathname);
  }

  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Report schedules</h1>
          <p className="mt-1 text-sm text-ink-muted">
            Reports emailed to staff on a schedule. Next run times are worked out by the server.
          </p>
        </div>
        <Link href="/staff/admin/report-schedules/new" className={buttonVariants()}>
          New schedule
        </Link>
      </header>

      <div className="flex flex-wrap gap-3">
        <div className="flex min-w-0 max-w-full flex-col gap-1">
          <label htmlFor="filter-status" className="text-sm font-semibold text-ink">
            Status
          </label>
          <select
            id="filter-status"
            value={status}
            onChange={(event) => write({ status: event.target.value, page: '' })}
            className={SELECT}
          >
            <option value="">All statuses</option>
            {SCHEDULE_STATUSES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>
        <div className="flex min-w-0 max-w-full flex-col gap-1">
          <label htmlFor="filter-report" className="text-sm font-semibold text-ink">
            Report
          </label>
          <select
            id="filter-report"
            value={reportKey}
            onChange={(event) => write({ reportKey: event.target.value, page: '' })}
            className={SELECT}
          >
            <option value="">All reports</option>
            {(catalogue.data ?? [])
              .filter((entry) => entry.canSchedule)
              .map((entry) => (
                <option key={entry.key} value={entry.key}>
                  {entry.title}
                </option>
              ))}
          </select>
        </div>
      </div>

      {list.isPending ? (
        <LoadingState variant="table" rows={5} label="Loading schedules…" />
      ) : list.isError ? (
        <ReportErrorView error={list.error} onRetry={() => void list.refetch()} />
      ) : (
        <>
          <ScheduleTable items={list.data.items} />
          <SimplePager
            pageNumber={list.data.pageNumber}
            pageSize={list.data.pageSize}
            totalCount={list.data.totalCount}
            onPageChange={(page) => write({ page: page > 1 ? String(page) : '' })}
          />
        </>
      )}
    </div>
  );
}

/** `/staff/admin/report-schedules` — HO-RPT-002 §11.5, US-RPT-06. Administrator only (server `403` -> `PermissionDeniedState`). */
export default function ReportSchedulesPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={5} label="Loading schedules…" />}>
      <ScheduleListContent />
    </Suspense>
  );
}
