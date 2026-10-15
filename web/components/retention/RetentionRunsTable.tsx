'use client';

import { useState } from 'react';
import { CircleAlert, CircleCheck, Clock, LoaderCircle } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { Badge } from '@/components/ui/badge';
import type { RetentionRunStatus, RetentionRunSummary } from '@/lib/api/retention';
import { formatInZone } from '@/lib/format/time';
import { RUN_STATUS_LABEL } from '@/lib/retention/labels';

export const RUN_STATUS_ICON: Record<RetentionRunStatus, React.ReactNode> = {
  Requested: <Clock size={12} aria-hidden="true" />,
  Running: <LoaderCircle size={12} aria-hidden="true" />,
  Succeeded: <CircleCheck size={12} aria-hidden="true" />,
  CompletedWithErrors: <CircleAlert size={12} aria-hidden="true" />,
};

export function RunStatusBadge({ status }: { status: RetentionRunStatus }) {
  return (
    <Badge
      variant={status === 'CompletedWithErrors' ? 'warning' : status === 'Succeeded' ? 'success' : 'neutral'}
      icon={RUN_STATUS_ICON[status]}
    >
      {RUN_STATUS_LABEL[status]}
    </Badge>
  );
}

function duration(run: RetentionRunSummary): string {
  if (!run.startedOn || !run.finishedOn) return '—';
  const seconds = Math.max(0, Math.round((Date.parse(run.finishedOn) - Date.parse(run.startedOn)) / 1000));
  return seconds < 60 ? `${seconds} s` : `${Math.round(seconds / 60)} min`;
}

/**
 * `components/retention/RetentionRunsTable.tsx` — HO-XCT-002 §11.5, UI-4. Paged run history. The
 * API has no status filter on this list (HO-XCT-001 section 12.4), so the status filter here only
 * narrows the runs on the page being shown, and says so.
 */
export function RetentionRunsTable({
  runs,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  zone,
}: {
  runs: RetentionRunSummary[] | undefined;
  totalCount: number | undefined;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  zone: string;
}) {
  const [status, setStatus] = useState<RetentionRunStatus | ''>('');
  const rows = status ? runs?.filter((r) => r.status === status) : runs;

  const columns: Array<DataTableColumn<RetentionRunSummary>> = [
    { id: 'requested', header: 'Requested', cell: (r) => formatInZone(r.requestedOn, zone) },
    { id: 'trigger', header: 'Started by', cell: (r) => (r.trigger === 'Manual' ? 'Staff (run now)' : 'Nightly schedule') },
    { id: 'status', header: 'Status', cell: (r) => <RunStatusBadge status={r.status} /> },
    { id: 'duration', header: 'Duration', cell: duration },
    { id: 'affected', header: 'Records changed', cell: (r) => `${r.totalAffected}${r.totalFailed ? ` · ${r.totalFailed} problems` : ''}` },
  ];

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <label htmlFor="run-status-filter" className="font-semibold text-ink">
          Show
        </label>
        <select
          id="run-status-filter"
          value={status}
          onChange={(e) => setStatus(e.target.value as RetentionRunStatus | '')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-ink"
        >
          <option value="">All runs on this page</option>
          {(Object.keys(RUN_STATUS_LABEL) as RetentionRunStatus[]).map((s) => (
            <option key={s} value={s}>
              {RUN_STATUS_LABEL[s]}
            </option>
          ))}
        </select>
      </div>
      <DataTable
        caption="Retention runs, newest first"
        columns={columns}
        rows={rows}
        getRowId={(r) => r.retentionRunId}
        rowHref={(r) => `/staff/admin/retention/runs/${r.retentionRunId}`}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{
          title: 'No retention runs yet',
          description: 'The nightly run appears here after its first pass. You can also run it now.',
        }}
        pagination={totalCount !== undefined ? { totalCount, pageSize } : undefined}
      />
    </div>
  );
}
