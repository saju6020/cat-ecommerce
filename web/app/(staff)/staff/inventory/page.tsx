'use client';

import { InventoryOverview } from '@/components/inventory/InventoryOverview';
import { PageHeading } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { useAlertSummary, useNearExpiryReport } from '@/lib/hooks/useInventory';
import { useRecalls } from '@/lib/hooks/useInventoryRecalls';

/** `/staff/inventory` — the overview: alerts, near-expiry teaser, recalls, stock search. A tile whose
 *  data the server refuses (`403`) is left out; the alert summary is what gates the page itself. */
export default function InventoryOverviewPage() {
  const alerts = useAlertSummary();
  const nearExpiry = useNearExpiryReport({});
  const recalls = useRecalls({ status: 'Active', pageNumber: 1, pageSize: 1 });
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Inventory" description="What needs attention at this branch, and a way into stock." />
      <QueryBoundary query={alerts}>
        {(summary) => (
          <InventoryOverview alerts={summary} nearExpiry={nearExpiry.data} activeRecalls={recalls.data?.totalCount} />
        )}
      </QueryBoundary>
    </div>
  );
}
