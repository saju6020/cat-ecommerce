'use client';

import { Suspense } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { STOCK_CLASS_OPTIONS } from '@/components/inventory/labels';
import { StockTable } from '@/components/stock/StockTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useStockSummary } from '@/lib/hooks/useInventory';
import { useLocations } from '@/lib/hooks/useInventoryLookup';

const FIELDS = ['search', 'locationId', 'class', 'belowReorderPoint'] as const;

/** `/staff/inventory/stock` — US-IMP-06/08: stock by item, with sellable kept apart from on hand. */
function StockContent() {
  const list = useInventoryListState(FIELDS, { debounced: ['search'] });
  const query = useStockSummary({
    ...list.params,
    belowReorderPoint: list.values.belowReorderPoint === 'true',
  });
  const locations = useLocations();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Stock"
        description="Sellable excludes expired, quarantined, damaged and reserved stock. All figures are the server's."
      />
      <FilterBar
        label="Filter stock"
        values={list.values}
        onChange={list.setValue}
        onCommit={list.flush}
        fields={[
          { name: 'search', label: 'Search by name or code', type: 'text' },
          {
            name: 'locationId',
            label: 'Location',
            type: 'select',
            options: [{ value: '', label: 'All locations' }, ...(locations.data ?? []).map((l) => ({ value: l.id, label: l.name }))],
          },
          { name: 'class', label: 'Class', type: 'select', options: STOCK_CLASS_OPTIONS },
          { name: 'belowReorderPoint', label: 'Below reorder point', type: 'checkbox' },
        ]}
      />
      <StockTable
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

export default function StockPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <StockContent />
    </Suspense>
  );
}
