'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { countryOptions } from '@/components/inventory/countries';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { CONSIGNMENT_STATUS_LABELS } from '@/components/inventory/labels';
import { ConsignmentTable } from '@/components/procurement/ConsignmentTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useConsignments } from '@/lib/hooks/useProcurement';

const FIELDS = ['status', 'originCountryCode', 'from', 'to'] as const;

/** `/staff/procurement/consignments` — US-IMP-03. */
function ConsignmentsContent() {
  const list = useInventoryListState(FIELDS);
  const query = useConsignments(list.params);

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Import consignments"
        description="Shipments from abroad, with their permits, certificates, costs and the batches that arrived."
        actions={
          <Link href="/staff/procurement/consignments/new" className={buttonVariants({ variant: 'primary' })}>
            Plan a consignment
          </Link>
        }
      />
      <FilterBar
        label="Filter consignments"
        values={list.values}
        onChange={list.setValue}
        onCommit={list.flush}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [{ value: '', label: 'All' }, ...Object.entries(CONSIGNMENT_STATUS_LABELS).map(([value, label]) => ({ value, label }))],
          },
          {
            name: 'originCountryCode',
            label: 'Origin',
            type: 'select',
            options: [{ value: '', label: 'Any country' }, ...countryOptions()],
          },
          { name: 'from', label: 'Arriving from', type: 'date' },
          { name: 'to', label: 'Arriving to', type: 'date' },
        ]}
      />
      <ConsignmentTable
        rows={query.data?.items}
        totalCount={query.data?.totalCount ?? 0}
        pageSize={list.pageSize}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
      />
    </div>
  );
}

export default function ConsignmentsPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <ConsignmentsContent />
    </Suspense>
  );
}
