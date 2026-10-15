'use client';

import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { diagnosticKindLabel } from '@/lib/format/clinical';
import { formatInZone } from '@/lib/format/time';
import type { PendingDiagnosticItem } from '@/lib/api/diagnostics';

/**
 * `PendingDiagnosticsTable` — HO-EMR-002 §11.9 (UI-5, US-CLN-14). Diagnostic orders still waiting for
 * results at this branch: cat (links to the order), kind, ordered (branch zone), ordered by. The parent
 * polls every 30 s without resetting anything.
 */
export function PendingDiagnosticsTable({
  rows,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  rows: PendingDiagnosticItem[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<DataTableColumn<PendingDiagnosticItem>> = [
    { id: 'cat', header: 'Cat', cell: (row) => row.catName },
    { id: 'kind', header: 'Test', cell: (row) => diagnosticKindLabel(row.kind) },
    { id: 'ordered', header: 'Ordered', cell: (row) => formatInZone(row.orderedOn, zone) },
    { id: 'by', header: 'Ordered by', cell: (row) => row.orderedBy },
  ];
  return (
    <DataTable
      caption="Diagnostic orders waiting for results"
      columns={columns}
      rows={rows}
      getRowId={(row) => row.id}
      rowHref={(row) => `/staff/diagnostics/${row.id}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      errorTitle="We couldn’t load the pending orders just now."
      emptyState={{
        title: 'No diagnostic orders are waiting.',
        description:
          'Orders appear here from the moment they are placed until results are entered.',
      }}
    />
  );
}
