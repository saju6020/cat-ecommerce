'use client';

import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { STOCK_CLASS_OPTIONS } from '@/components/inventory/labels';
import { StockOnHandReport } from '@/components/stock/StockOnHandReport';
import { getStockOnHandCsvPath } from '@/lib/api/inventory-reports';
import { useStockOnHandReport, useUnitBySku } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';
import { useLocations } from '@/lib/hooks/useInventoryLookup';

const FIELDS = ['locationId', 'class'] as const;

/** `/staff/inventory/reports/stock-on-hand` — item × batch × location with cost and value. */
function StockOnHandContent() {
  const list = useInventoryListState(FIELDS);
  const params = { locationId: list.values.locationId || undefined, class: list.values.class || undefined };
  const query = useStockOnHandReport(params);
  const locations = useLocations();
  const units = useUnitBySku();
  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        label="Stock-on-hand filters"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'locationId',
            label: 'Location',
            type: 'select',
            options: [{ value: '', label: 'All locations' }, ...(locations.data ?? []).map((l) => ({ value: l.id, label: l.name }))],
          },
          { name: 'class', label: 'Class', type: 'select', options: STOCK_CLASS_OPTIONS },
        ]}
      />
      <QueryBoundary query={query}>
        {(report) => <StockOnHandReport report={report} unitBySku={units} csvPath={getStockOnHandCsvPath(params)} />}
      </QueryBoundary>
    </div>
  );
}

export default function StockOnHandPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <StockOnHandContent />
    </Suspense>
  );
}
