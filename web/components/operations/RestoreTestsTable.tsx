'use client';

import { CircleCheck, CircleX } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { Badge } from '@/components/ui/badge';
import type { RestoreTestOutcome, RestoreTestSummary } from '@/lib/api/operations';
import { formatDateOnly } from '@/lib/format/time';

export function OutcomeBadge({ outcome }: { outcome: RestoreTestOutcome }) {
  return outcome === 'Passed' ? (
    <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>Passed</Badge>
  ) : (
    <Badge variant="danger" icon={<CircleX size={12} aria-hidden="true" />}>Failed</Badge>
  );
}

/** `components/operations/RestoreTestsTable.tsx` — HO-XCT-002 §11.5, UI-4. Paged history of
 *  restore tests: date, environment, outcome, RPO, RTO (the server's values). The performer is
 *  an id on the wire (no name), so the list does not show one; the detail page does not either. */
export function RestoreTestsTable({
  tests,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
}: {
  tests: RestoreTestSummary[] | undefined;
  totalCount: number | undefined;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<DataTableColumn<RestoreTestSummary>> = [
    { id: 'date', header: 'Date', cell: (t) => formatDateOnly(t.performedOn) },
    { id: 'environment', header: 'Environment', cell: (t) => t.environment },
    { id: 'outcome', header: 'Outcome', cell: (t) => <OutcomeBadge outcome={t.outcome} /> },
    { id: 'rpo', header: 'Data lost (RPO)', cell: (t) => `${t.achievedRpoMinutes} min` },
    { id: 'rto', header: 'Time to restore (RTO)', cell: (t) => `${t.achievedRtoMinutes} min` },
    { id: 'set', header: 'Backup set', cell: (t) => t.backupSetLabel },
  ];
  return (
    <DataTable
      caption="Restore tests, newest first"
      columns={columns}
      rows={tests}
      getRowId={(t) => t.id}
      rowHref={(t) => `/staff/admin/operations/backup/${t.id}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{
        title: 'No restore test recorded yet',
        description: 'Run the restore drill and record its output with the form below.',
      }}
      pagination={totalCount !== undefined ? { totalCount, pageSize } : undefined}
    />
  );
}
