'use client';

import { Suspense } from 'react';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ReorderSuggestions } from '@/components/procurement/ReorderSuggestions';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useLocations } from '@/lib/hooks/useInventoryLookup';
import { useReorderSuggestions, useSuppliers } from '@/lib/hooks/useProcurement';

const FIELDS = ['locationId', 'supplierId'] as const;

/** `/staff/procurement/reorder` — US-IMP-12: suggestions → build a purchase order. */
function ReorderContent() {
  const list = useInventoryListState(FIELDS);
  const params = { locationId: list.values.locationId || undefined, supplierId: list.values.supplierId || undefined };
  const query = useReorderSuggestions(params);
  const locations = useLocations();
  const suppliers = useSuppliers({ isActive: 'true', pageSize: 100 });
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Reorder suggestions"
        description="Items at or below their reorder point, with a suggested quantity worked out by the server."
      />
      <FilterBar
        label="Filter reorder suggestions"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'locationId',
            label: 'Location',
            type: 'select',
            options: [{ value: '', label: 'All locations' }, ...(locations.data ?? []).map((l) => ({ value: l.id, label: l.name }))],
          },
          {
            name: 'supplierId',
            label: 'Preferred supplier',
            type: 'select',
            options: [{ value: '', label: 'All suppliers' }, ...(suppliers.data?.items ?? []).map((s) => ({ value: s.id, label: s.name }))],
          },
        ]}
      />
      <QueryBoundary query={query}>
        {(data) =>
          data.items.length === 0 ? (
            <EmptyState title="Nothing needs reordering" description="Every item is above its reorder point." />
          ) : (
            <ReorderSuggestions items={data.items} />
          )
        }
      </QueryBoundary>
    </div>
  );
}

export default function ReorderPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <ReorderContent />
    </Suspense>
  );
}
