'use client';

import { use, useState } from 'react';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { ConsignmentCosts } from '@/components/procurement/ConsignmentCosts';
import { LandedCostPanel } from '@/components/procurement/LandedCostPanel';
import { todayInZone } from '@/lib/format/time';
import { FALLBACK_CURRENCY_CODE } from '@/lib/shop/currency';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useConsignment, useConsignmentActions, useInspectionPack } from '@/lib/hooks/useProcurement';

/** The base (commerce) currency the costs are converted to — the server's, as named by the landed-
 *  cost payload when it has one; otherwise the branch's usual currency. */
const FALLBACK_BASE_CURRENCY = FALLBACK_CURRENCY_CODE;

/** `/staff/procurement/consignments/[consignmentId]/costs` — costs, rates and landed cost
 *  (US-IMP-03, US-IMP-05). */
export default function ConsignmentCostsPage({ params }: { params: Promise<{ consignmentId: string }> }) {
  const { consignmentId } = use(params);
  const query = useConsignment(consignmentId);
  const pack = useInspectionPack(consignmentId);
  const actions = useConsignmentActions(consignmentId);
  const zone = useBranchTimeZone();
  // The form keeps what was typed through a save; only an explicit Reload replaces it with the server's.
  const [reloadKey, setReloadKey] = useState(0);
  return (
    <QueryBoundary query={query}>
      {(c) => {
        const canEdit = c.allowedActions.includes('costs');
        const batches = pack.data?.batches ?? [];
        const unitsByBatch = Object.fromEntries(batches.map((b) => [b.batchId, b.stockUnit]));
        return (
          <div className="flex flex-col gap-5">
            <ConsignmentCosts
              key={reloadKey}
              costComponents={c.costComponents}
              exchangeRates={c.exchangeRates}
              version={c.version}
              baseCurrency={c.landedCost.totalCostsBase?.currencyCode ?? FALLBACK_BASE_CURRENCY}
              today={todayInZone(zone)}
              readOnly={!canEdit}
              isPending={actions.replaceCosts.isPending}
              onReload={() => void query.refetch().then(() => setReloadKey((k) => k + 1))}
              onSave={(request) => actions.replaceCosts.mutateAsync(request)}
            />
            <LandedCostPanel
              landed={c.landedCost}
              unitsByBatch={unitsByBatch}
              batchCount={batches.length}
              canAllocate={canEdit}
              timeZoneId={zone}
              onAllocate={() => actions.allocate.mutateAsync(c.version)}
            />
          </div>
        );
      }}
    </QueryBoundary>
  );
}
