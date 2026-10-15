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
import type { ReportScheduleListItem } from '@/lib/api/report-schedules';
import { scheduleTimingLabel } from '@/lib/format/report';
import { formatInZone } from '@/lib/format/time';
import { useNarrowContainer } from '@/lib/hooks/useIsNarrow';
import { RunStatusBadge, ScheduleStatusBadge } from './ScheduleStatusBadge';

function NextRun({ item }: { item: ReportScheduleListItem }) {
  return item.nextRunOn ? <>{formatInZone(item.nextRunOn, item.timeZoneId)}</> : <>—</>;
}

function LastRun({ item }: { item: ReportScheduleListItem }) {
  if (!item.lastRunOn) return <>Not run yet</>;
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      {formatInZone(item.lastRunOn, item.timeZoneId)}
      {item.lastRunStatus && <RunStatusBadge status={item.lastRunStatus} />}
    </span>
  );
}

/**
 * `components/report-schedules/ScheduleTable.tsx` — HO-RPT-002 §11.5, UI-4. Name, report, branch,
 * "Daily at 07:00 (Asia/Dhaka)", format, recipient count, status (icon + word), the server's next
 * run and the last run with its status. Cards below the content-width floor (UI-1).
 */
export function ScheduleTable({ items }: { items: ReportScheduleListItem[] }) {
  const [ref, narrow] = useNarrowContainer<HTMLDivElement>();

  if (items.length === 0) {
    return (
      <EmptyState
        title="No schedules match"
        description="Clear the filters, or open a report and choose Schedule this report."
      />
    );
  }

  return (
    <div ref={ref}>
      {narrow ? (
        <ul aria-label="Report schedules" className="m-0 flex list-none flex-col gap-3 p-0">
          {items.map((item) => (
            <li
              key={item.id}
              className="flex flex-col gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm"
            >
              <Link
                href={`/staff/admin/report-schedules/${item.id}`}
                className="font-bold text-accent-ink underline"
              >
                {item.name}
              </Link>
              <ScheduleStatusBadge status={item.status} />
              <span>
                {item.reportTitle} · {item.branchName}
              </span>
              <span>
                {scheduleTimingLabel(item.frequencyLabel, item.sendAtLocal, item.timeZoneId)}
              </span>
              <span>
                {item.format.toUpperCase()} · {item.recipientCount} recipients
              </span>
              <span>
                Next run: <NextRun item={item} />
              </span>
              <span>
                Last run: <LastRun item={item} />
              </span>
            </li>
          ))}
        </ul>
      ) : (
        <Table>
          <TableCaption>Report schedules</TableCaption>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Report</TableHead>
              <TableHead>Branch</TableHead>
              <TableHead>When</TableHead>
              <TableHead>Format</TableHead>
              <TableHead className="text-right">Recipients</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Next run</TableHead>
              <TableHead>Last run</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={item.id}>
                <TableCell>
                  <Link
                    href={`/staff/admin/report-schedules/${item.id}`}
                    className="font-semibold text-accent-ink underline"
                  >
                    {item.name}
                  </Link>
                </TableCell>
                <TableCell>{item.reportTitle}</TableCell>
                <TableCell>{item.branchName}</TableCell>
                <TableCell>
                  {scheduleTimingLabel(item.frequencyLabel, item.sendAtLocal, item.timeZoneId)}
                </TableCell>
                <TableCell>{item.format.toUpperCase()}</TableCell>
                <TableCell className="text-right tabular-nums">{item.recipientCount}</TableCell>
                <TableCell>
                  <ScheduleStatusBadge status={item.status} />
                </TableCell>
                <TableCell>
                  <NextRun item={item} />
                </TableCell>
                <TableCell>
                  <LastRun item={item} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
