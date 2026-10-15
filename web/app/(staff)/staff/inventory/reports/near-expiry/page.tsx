'use client';

import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { FilterBar } from '@/components/inventory/FilterBar';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { NearExpiryReport } from '@/components/stock/NearExpiryReport';
import { getNearExpiryCsvPath } from '@/lib/api/inventory-reports';
import { useNearExpiryReport, useUnitBySku } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';

const FIELDS = ['withinDays', 'includeExpired'] as const;

/** `/staff/inventory/reports/near-expiry` — US-IMP-10 AC2. The window defaults to the server's own
 *  (its largest configured threshold, OQ-3): with none chosen, none is sent. */
function NearExpiryContent() {
  const list = useInventoryListState(FIELDS);
  const params = {
    // `list.params` is the settled (debounced) copy, so typing "90" is one request, not two.
    withinDays: list.params.withinDays ? parseInt(String(list.params.withinDays), 10) : undefined,
    includeExpired: list.params.includeExpired === 'true',
  };
  const query = useNearExpiryReport(params);
  const units = useUnitBySku();
  return (
    <div className="flex flex-col gap-4">
      <FilterBar
        label="Near-expiry window"
        values={{ ...list.values, withinDays: list.values.withinDays || (query.data ? String(query.data.withinDays) : '') }}
        onChange={list.setValue}
        fields={[
          { name: 'withinDays', label: 'Expiring within (days)', type: 'number', min: 0, max: 3650 },
          { name: 'includeExpired', label: 'Include expired stock', type: 'checkbox' },
        ]}
      />
      <QueryBoundary query={query}>
        {(report) => <NearExpiryReport report={report} unitBySku={units} csvPath={getNearExpiryCsvPath(params)} />}
      </QueryBoundary>
    </div>
  );
}

export default function NearExpiryPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <NearExpiryContent />
    </Suspense>
  );
}
