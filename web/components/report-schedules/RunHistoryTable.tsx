'use client';

import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { ReportScheduleRun } from '@/lib/api/report-schedules';
import { describeRunFailure, formatFileSize, formatReportDate } from '@/lib/format/report';
import { formatInZone } from '@/lib/format/time';
import { useNarrowContainer } from '@/lib/hooks/useIsNarrow';
import { statusLabel } from '@/components/notifications/statusText';
import { RunStatusBadge } from './ScheduleStatusBadge';

function Detail({ run, timeZoneId }: { run: ReportScheduleRun; timeZoneId: string }) {
  return (
    <div className="flex flex-col gap-1 text-sm">
      {run.status === 'Failed' && (
        <p className="m-0 font-semibold text-danger-ink">{describeRunFailure(run.failureCode)}</p>
      )}
      {run.status === 'Purged' && run.purgedOn && (
        <p className="m-0 text-ink-muted">
          The file was removed on {formatInZone(run.purgedOn, timeZoneId)}.
        </p>
      )}
      {run.attachmentOmitted && (
        <p className="m-0 text-ink-muted">Too large to attach — link sent</p>
      )}
      {run.deliveries.length > 0 && (
        <ul className="m-0 list-none p-0">
          {run.deliveries.map((delivery, index) => (
            <li key={`${delivery.recipientName}-${index}`}>
              {delivery.recipientName}: {statusLabel(delivery.status)}
            </li>
          ))}
        </ul>
      )}
      {run.status === 'Generated' && run.deliveries.length > 0 && (
        <Link href="/staff/notifications" className="font-semibold text-accent-ink underline">
          See the message log
        </Link>
      )}
    </div>
  );
}

/**
 * `components/report-schedules/RunHistoryTable.tsx` — HO-RPT-002 §11.5, U-E7. One row per run: due
 * time (in the schedule's zone), trigger, status as text (Generated / Failed / Purged), the period
 * covered, rows, file size, and per-recipient delivery status. A `Failed` run says what to change;
 * `attachmentOmitted` says "Too large to attach — link sent". Cards below the content-width floor.
 */
export function RunHistoryTable({
  runs,
  timeZoneId,
}: {
  runs: ReportScheduleRun[];
  timeZoneId: string;
}) {
  const [ref, narrow] = useNarrowContainer<HTMLDivElement>();

  if (runs.length === 0) {
    return (
      <EmptyState
        title="No runs yet"
        description="A run appears here when the schedule is due, or when you use Run now."
      />
    );
  }

  return (
    <div ref={ref}>
      {narrow ? (
        <ul aria-label="Run history" className="m-0 flex list-none flex-col gap-3 p-0">
          {runs.map((run) => (
            <li
              key={run.runId}
              className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-3"
            >
              <div className="flex flex-wrap items-center gap-2">
                <RunStatusBadge status={run.status} />
                <span className="text-sm text-ink-muted">
                  {run.trigger === 'Manual' ? 'Manual' : 'Scheduled'} ·{' '}
                  {formatInZone(run.dueOn, timeZoneId)}
                </span>
              </div>
              <p className="m-0 text-sm text-ink">
                Covers {formatReportDate(run.periodFrom)} to {formatReportDate(run.periodTo)} ·{' '}
                {run.rowCount} rows · {formatFileSize(run.contentLength)}
              </p>
              <Detail run={run} timeZoneId={timeZoneId} />
            </li>
          ))}
        </ul>
      ) : (
        <Table>
          <TableCaption>Run history (times in {timeZoneId})</TableCaption>
          <TableHeader>
            <TableRow>
              <TableHead>Due</TableHead>
              <TableHead>Trigger</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Period covered</TableHead>
              <TableHead className="text-right">Rows</TableHead>
              <TableHead className="text-right">File size</TableHead>
              <TableHead>Delivery</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {runs.map((run) => (
              <TableRow key={run.runId}>
                <TableCell>{formatInZone(run.dueOn, timeZoneId)}</TableCell>
                <TableCell>{run.trigger === 'Manual' ? 'Manual' : 'Scheduled'}</TableCell>
                <TableCell>
                  <RunStatusBadge status={run.status} />
                </TableCell>
                <TableCell>
                  {formatReportDate(run.periodFrom)} to {formatReportDate(run.periodTo)}
                </TableCell>
                <TableCell className="text-right tabular-nums">{run.rowCount}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatFileSize(run.contentLength)}
                </TableCell>
                <TableCell>
                  <Detail run={run} timeZoneId={timeZoneId} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
