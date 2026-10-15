'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { LoadingState } from '@/components/common/LoadingState';
import { SimplePager } from '@/components/notifications/SimplePager';
import { ReportErrorView } from '@/components/reports/ReportErrorView';
import { RunHistoryTable } from '@/components/report-schedules/RunHistoryTable';
import { ScheduleActions } from '@/components/report-schedules/ScheduleActions';
import { ScheduleForm } from '@/components/report-schedules/ScheduleForm';
import { ScheduleStatusBadge } from '@/components/report-schedules/ScheduleStatusBadge';
import { formatInZone } from '@/lib/format/time';
import { useBranchOptions } from '@/lib/hooks/useBranchOptions';
import { useReportCatalogue } from '@/lib/hooks/useReports';
import { useSaveSchedule, useSchedule, useScheduleRuns } from '@/lib/hooks/useReportSchedules';
import { fromScheduleDetail, toScheduleInput } from '@/lib/validation/reportSchedule';

/**
 * `/staff/admin/report-schedules/[scheduleId]` — HO-RPT-002 §11.5, US-RPT-06. The schedule, its
 * actions (Pause / Resume / Cancel / Run now), the edit form (saved with `expectedVersion`, a stale
 * version offers a reload) and the paged run history. A cancelled schedule is read-only.
 */
export default function ReportSchedulePage() {
  const { scheduleId } = useParams<{ scheduleId: string }>();
  const [runPage, setRunPage] = useState(1);
  const schedule = useSchedule(scheduleId);
  const runs = useScheduleRuns(scheduleId, runPage);
  const catalogue = useReportCatalogue();
  const { options, zoneFor } = useBranchOptions();
  const save = useSaveSchedule(scheduleId);

  if (schedule.isPending || catalogue.isPending) {
    return <LoadingState variant="card" rows={6} label="Loading schedule…" />;
  }
  if (schedule.isError) {
    return <ReportErrorView error={schedule.error} onRetry={() => void schedule.refetch()} />;
  }
  if (catalogue.isError) return <ReportErrorView error={catalogue.error} />;

  const detail = schedule.data;
  const reports = catalogue.data.filter((entry) => entry.canSchedule);
  const cancelled = detail.status === 'Cancelled';

  return (
    <div className="flex flex-col gap-6">
      <Link
        href="/staff/admin/report-schedules"
        className="text-sm font-semibold text-accent-ink underline"
      >
        <span aria-hidden="true">← </span>Report schedules
      </Link>

      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="font-display text-2xl font-semibold text-ink">{detail.name}</h1>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
            <ScheduleStatusBadge status={detail.status} />
            {detail.reportTitle} · {detail.branchName} · {detail.frequencyLabel} at{' '}
            {detail.sendAtLocal} ({detail.timeZoneId})
            {detail.nextRunOn && (
              <> · next run {formatInZone(detail.nextRunOn, detail.timeZoneId)}</>
            )}
          </p>
        </div>
        <ScheduleActions
          scheduleId={detail.id}
          name={detail.name}
          status={detail.status}
          recipientCount={detail.recipients.length}
        />
      </header>

      {cancelled ? (
        <p role="status" className="m-0 text-sm text-ink-muted">
          This schedule was cancelled and cannot be changed. Its past runs are below.
        </p>
      ) : (
        <ScheduleForm
          key={detail.version}
          reports={reports}
          initial={fromScheduleDetail(detail)}
          keptRecipients={detail.recipients}
          branches={options}
          zoneFor={zoneFor}
          isPending={save.isPending}
          submitLabel="Save changes"
          onReload={() => void schedule.refetch()}
          onSubmit={(values) => save.mutateAsync(toScheduleInput(values, detail.version))}
        />
      )}

      <section aria-labelledby="runs-heading" className="flex flex-col gap-3">
        <h2 id="runs-heading" className="text-lg font-bold text-ink">
          Run history
        </h2>
        {runs.isPending ? (
          <LoadingState variant="table" rows={3} label="Loading run history…" />
        ) : runs.isError ? (
          <ReportErrorView error={runs.error} onRetry={() => void runs.refetch()} />
        ) : (
          <>
            <RunHistoryTable runs={runs.data.items} timeZoneId={detail.timeZoneId} />
            <SimplePager
              pageNumber={runs.data.pageNumber}
              pageSize={runs.data.pageSize}
              totalCount={runs.data.totalCount}
              onPageChange={setRunPage}
            />
          </>
        )}
      </section>
    </div>
  );
}
