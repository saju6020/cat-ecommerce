'use client';

import { use } from 'react';
import { useRouter } from 'next/navigation';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { BatchDetail } from '@/components/stock/BatchDetail';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useBatch, useBatchActions } from '@/lib/hooks/useInventory';
import { useDeclareRecall } from '@/lib/hooks/useInventoryRecalls';

/** `/staff/inventory/batches/[batchId]` — US-IMP-06/09/11. */
export default function BatchPage({ params }: { params: Promise<{ batchId: string }> }) {
  const { batchId } = use(params);
  const router = useRouter();
  const query = useBatch(batchId);
  const actions = useBatchActions(batchId);
  const declare = useDeclareRecall(batchId);
  const zone = useBranchTimeZone();
  return (
    <QueryBoundary query={query} notFoundTitle="This batch could not be found" notFoundDescription="It may belong to another branch.">
      {(batch) => (
        <BatchDetail
          batch={batch}
          timeZoneId={zone}
          onQuarantine={(reason) => actions.quarantine.mutateAsync(reason)}
          onRelease={() => actions.release.mutateAsync()}
          onWriteOff={(reason) => actions.writeOff.mutateAsync(reason)}
          onDeclareRecall={async (request) => {
            await declare.mutateAsync(request);
            router.push(`/staff/inventory/recalls/${batch.id}`);
          }}
        />
      )}
    </QueryBoundary>
  );
}
