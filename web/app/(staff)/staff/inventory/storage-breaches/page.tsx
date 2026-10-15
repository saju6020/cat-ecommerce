'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useBreaches } from '@/lib/hooks/useInventoryRecalls';

const FIELDS = ['status'] as const;
const STATUS_LABELS = { Open: 'Open', Quarantined: 'Stock quarantined', Closed: 'Closed' } as const;

/** `/staff/inventory/storage-breaches` — US-IMP-16 AC2. */
function BreachesContent() {
  const list = useInventoryListState(FIELDS);
  const query = useBreaches(list.params);
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Storage breaches"
        description="Times a product was kept outside its required conditions."
        actions={
          <Link href="/staff/inventory/storage-breaches/new" className={buttonVariants({ variant: 'primary' })}>
            Report a breach
          </Link>
        }
      />
      <FilterBar
        label="Filter storage breaches"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [{ value: '', label: 'All' }, ...Object.entries(STATUS_LABELS).map(([value, label]) => ({ value, label }))],
          },
        ]}
      />
      <DataTable
        caption="Storage breaches"
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        rowHref={(row) => `/staff/inventory/storage-breaches/${row.id}`}
        emptyState={{ title: 'No storage breaches', description: 'Report one if a product is kept outside its conditions.' }}
        columns={[
          { id: 'observed', header: 'Observed', cell: (row) => formatInZone(row.observedOn, zone) },
          { id: 'loc', header: 'Location', cell: (row) => row.locationCode ?? '—' },
          { id: 'what', header: 'What happened', cell: (row) => row.condition },
          { id: 'batches', header: 'Batches flagged', cell: (row) => `${row.affectedBatchCount} ${row.affectedBatchCount === 1 ? 'batch' : 'batches'}` },
          {
            id: 'status',
            header: 'Status',
            cell: (row) => <Badge variant={row.status === 'Open' ? 'warning' : 'neutral'}>{STATUS_LABELS[row.status]}</Badge>,
          },
        ]}
      />
    </div>
  );
}

export default function BreachesPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <BreachesContent />
    </Suspense>
  );
}
