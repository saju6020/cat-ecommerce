'use client';

import { use } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { RecallConsole } from '@/components/recall/RecallConsole';
import { getRecallTraceCsvUrl } from '@/lib/api/inventory-recalls';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useSkusByCode } from '@/lib/hooks/useInventoryLookup';
import { useRecall, useRecallActions, useRecallTrace } from '@/lib/hooks/useInventoryRecalls';

/** `/staff/inventory/recalls/[batchId]` — US-IMP-11: the recall console (stock, trace, notify). Only
 *  `Recalls.Manage` can read it; anyone else gets the server's `403`. */
export default function RecallPage({ params }: { params: Promise<{ batchId: string }> }) {
  const { batchId } = use(params);
  const detail = useRecall(batchId);
  const trace = useRecallTrace(batchId);
  const actions = useRecallActions(batchId);
  const zone = useBranchTimeZone();
  const sku = useSkusByCode(detail.data ? [detail.data.summary.skuCode] : [])[0]?.data;
  return (
    <QueryBoundary query={detail} notFoundTitle="This recall could not be found" notFoundDescription="Only a batch that has been recalled has a recall console.">
      {(d) => (
        <QueryBoundary query={trace} loadingLabel="Loading the trace…">
          {(t) =>
            sku ? (
              <RecallConsole
                detail={d}
                trace={t}
                stockUnit={sku.stockUnit}
                csvUrl={getRecallTraceCsvUrl(batchId)}
                timeZoneId={zone}
                onNotify={() => actions.notify.mutateAsync()}
                onWithdraw={(reason) => actions.withdraw.mutateAsync(reason)}
                onClose={(note) => actions.close.mutateAsync(note)}
              />
            ) : (
              <LoadingState rows={4} />
            )
          }
        </QueryBoundary>
      )}
    </QueryBoundary>
  );
}
