'use client';

import { Suspense } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { AlertList } from '@/components/inventory/AlertList';
import { FilterBar } from '@/components/inventory/FilterBar';
import { PageHeading } from '@/components/inventory/fields';
import { ALERT_KIND_OPTIONS } from '@/components/inventory/labels';
import { ApiForbiddenError } from '@/lib/api/client';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useAcknowledgeAlert, useAlerts } from '@/lib/hooks/useInventory';
import { useInventoryListState } from '@/lib/hooks/useInventoryListState';

const FIELDS = ['status', 'kind'] as const;

/** `/staff/inventory/alerts` — US-IMP-10/12. The nav badge is the polled count (ADR-FE-045); this
 *  page lists the alerts themselves. */
function AlertsContent() {
  const list = useInventoryListState(FIELDS, { defaults: { status: 'Open' } });
  // "All" is a screen choice: without it, clearing the filter would fall back to the Open default.
  const { status, ...withoutStatus } = list.params;
  const query = useAlerts(status === 'all' ? withoutStatus : list.params);
  const acknowledge = useAcknowledgeAlert();
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Alerts" description="Expiry, low stock, shortfalls, storage breaches and ledger mismatches at this branch." />
      <FilterBar
        label="Filter alerts"
        values={list.values}
        onChange={list.setValue}
        fields={[
          {
            name: 'status',
            label: 'Status',
            type: 'select',
            options: [
              { value: 'all', label: 'All' },
              { value: 'Open', label: 'Open' },
              { value: 'Acknowledged', label: 'Acknowledged' },
              { value: 'Resolved', label: 'Resolved' },
            ],
          },
          { name: 'kind', label: 'Kind', type: 'select', options: ALERT_KIND_OPTIONS },
        ]}
      />
      <AlertList
        rows={query.data?.items}
        totalCount={query.data?.totalCount ?? 0}
        pageSize={list.pageSize}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        timeZoneId={zone}
        onAcknowledge={(id, note) => acknowledge.mutateAsync({ id, note })}
      />
    </div>
  );
}

export default function AlertsPage() {
  return (
    <Suspense fallback={<LoadingState rows={5} />}>
      <AlertsContent />
    </Suspense>
  );
}
