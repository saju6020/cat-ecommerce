'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { SupplierTable } from '@/components/procurement/SupplierTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useSuppliers } from '@/lib/hooks/useProcurement';

const FIELDS = ['search', 'isActive'] as const;

/** `/staff/procurement/suppliers` — US-IMP-01. */
function SuppliersContent() {
  const list = useInventoryListState(FIELDS, { debounced: ['search'] });
  const query = useSuppliers(list.params);

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Suppliers"
        description="The companies you buy stock from. A supplier is shared by every branch."
        actions={
          <Link href="/staff/procurement/suppliers/new" className={buttonVariants({ variant: 'primary' })}>
            New supplier
          </Link>
        }
      />
      <FilterBar
        label="Filter suppliers"
        values={list.values}
        onChange={list.setValue}
        onCommit={list.flush}
        fields={[
          { name: 'search', label: 'Search by name or code', type: 'text' },
          {
            name: 'isActive',
            label: 'Status',
            type: 'select',
            options: [
              { value: '', label: 'All' },
              { value: 'true', label: 'Active' },
              { value: 'false', label: 'Inactive' },
            ],
          },
        ]}
      />
      <SupplierTable
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

export default function SuppliersPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <SuppliersContent />
    </Suspense>
  );
}
