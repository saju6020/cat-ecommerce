'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { DataTable } from '@/components/common/DataTable';
import { LoadingState } from '@/components/common/LoadingState';
import { PageHeading } from '@/components/inventory/fields';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useTransfers } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';

/** `/staff/inventory/transfers` — US-IMP-13: stock moved between locations. */
function TransfersContent() {
  const list = useInventoryListState([] as const);
  const query = useTransfers({ pageNumber: list.pageNumber, pageSize: list.pageSize });
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Transfers"
        description="Stock moved between locations. Each transfer writes two ledger entries, one at each end."
        actions={
          <Link href="/staff/inventory/transfers/new" className={buttonVariants({ variant: 'primary' })}>
            New transfer
          </Link>
        }
      />
      <DataTable
        caption="Stock transfers"
        rows={query.data?.items}
        getRowId={(row) => row.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        pagination={{ totalCount: query.data?.totalCount ?? 0, pageSize: list.pageSize }}
        emptyState={{ title: 'No transfers yet', description: 'Post a transfer to move stock between locations.' }}
        columns={[
          { id: 'number', header: 'Transfer', cell: (row) => row.number },
          { id: 'route', header: 'Route', cell: (row) => `${row.fromLocationCode ?? '?'} → ${row.toLocationCode ?? '?'}` },
          { id: 'when', header: 'When', cell: (row) => formatInZone(row.transferredOn, zone) },
          { id: 'lines', header: 'Lines', cell: (row) => `${row.lineCount} ${row.lineCount === 1 ? 'line' : 'lines'}` },
          { id: 'reason', header: 'Reason', cell: (row) => row.reason ?? '—' },
        ]}
      />
    </div>
  );
}

export default function TransfersPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <TransfersContent />
    </Suspense>
  );
}
