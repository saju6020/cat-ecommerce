'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { ApprovalQueue } from '@/components/inventory/ApprovalQueue';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { COUNT_SCOPE_LABELS, COUNT_STATUS_LABELS } from '@/components/inventory/labels';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatMoney } from '@/lib/format/money';
import { formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useAdjustments, useCounts } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useLocations } from '@/lib/hooks/useInventoryLookup';

const FIELDS = ['status', 'locationId'] as const;

/** `/staff/inventory/counts` — US-IMP-14: counts, with the shared approval queue. */
function CountsContent() {
  const list = useInventoryListState(FIELDS);
  const query = useCounts(list.params);
  const pending = useAdjustments({ status: 'PendingApproval', pageSize: 50 });
  const awaiting = useCounts({ status: 'AwaitingApproval', pageSize: 50 });
  const locations = useLocations();
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Stock counts"
        description="Count blind, review the variances, then commit. Nothing changes stock until a count is committed."
        actions={
          <Link href="/staff/inventory/counts/new" className={buttonVariants({ variant: 'primary' })}>
            Open a count
          </Link>
        }
      />
      <ApprovalQueue adjustments={pending.data?.items ?? []} counts={awaiting.data?.items ?? []} timeZoneId={zone} />
      <FilterBar
        label="Filter counts"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [{ value: '', label: 'All' }, ...Object.entries(COUNT_STATUS_LABELS).map(([value, label]) => ({ value, label }))],
          },
          {
            name: 'locationId',
            label: 'Location',
            type: 'select',
            options: [{ value: '', label: 'All locations' }, ...(locations.data ?? []).map((l) => ({ value: l.id, label: l.name }))],
          },
        ]}
      />
      <DataTable
        caption="Stock counts"
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        rowHref={(row) => `/staff/inventory/counts/${row.id}`}
        emptyState={{ title: 'No counts yet', description: 'Open a count to check what is really on the shelf.' }}
        columns={[
          { id: 'number', header: 'Count', cell: (row) => row.number },
          { id: 'location', header: 'Location', cell: (row) => row.locationCode ?? '—' },
          { id: 'scope', header: 'Scope', cell: (row) => COUNT_SCOPE_LABELS[row.scope] },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => (
              <Badge variant={row.status === 'AwaitingApproval' ? 'warning' : row.status === 'Committed' ? 'success' : 'neutral'}>
                {COUNT_STATUS_LABELS[row.status]}
              </Badge>
            ),
          },
          { id: 'lines', header: 'Lines', cell: (row) => `${row.lineCount} ${row.lineCount === 1 ? 'line' : 'lines'}` },
          { id: 'variance', header: 'Variance', cell: (row) => (row.varianceValueBase ? formatMoney(row.varianceValueBase) : '—'), cellClassName: 'tabular-nums' },
          { id: 'opened', header: 'Opened', cell: (row) => formatInZone(row.openedOn, zone) },
        ]}
      />
    </div>
  );
}

export default function CountsPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <CountsContent />
    </Suspense>
  );
}
