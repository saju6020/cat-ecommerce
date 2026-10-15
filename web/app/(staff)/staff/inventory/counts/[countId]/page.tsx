'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ActionDialog } from '@/components/inventory/ReasonDialog';
import { Banner, PageHeading } from '@/components/inventory/fields';
import { CountSheet } from '@/components/inventory/CountSheet';
import { CountVariance } from '@/components/inventory/CountVariance';
import { COUNT_SCOPE_LABELS, COUNT_STATUS_LABELS } from '@/components/inventory/labels';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { useCount, useCountActions, useCountVariance } from '@/lib/hooks/useInventory';
import { useBarcodeLookup } from '@/lib/hooks/useInventoryLookup';

/** `/staff/inventory/counts/[countId]` — US-IMP-14: the blind sheet while open, then the variance
 *  report, commit and approval. */
export default function CountPage({ params }: { params: Promise<{ countId: string }> }) {
  const { countId } = use(params);
  const count = useCount(countId);
  const actions = useCountActions(countId);
  const lookup = useBarcodeLookup();
  const hasReport = !!count.data && count.data.status !== 'Open' && count.data.status !== 'Cancelled';
  const variance = useCountVariance(countId, hasReport);
  const [cancelling, setCancelling] = useState(false);

  return (
    <QueryBoundary query={count} notFoundTitle="This count could not be found" notFoundDescription="It may belong to another branch.">
      {(c) => (
        <div className="flex flex-col gap-5">
          <PageHeading
            title={c.number}
            description={
              <span className="flex flex-wrap items-center gap-2">
                <Badge variant={c.status === 'AwaitingApproval' ? 'warning' : c.status === 'Committed' ? 'success' : 'neutral'}>
                  {COUNT_STATUS_LABELS[c.status]}
                </Badge>
                <span>
                  {c.locationCode ?? 'a location'} · {COUNT_SCOPE_LABELS[c.scope]}
                </span>
              </span>
            }
            actions={
              <>
                <Link href="/staff/inventory/counts" className="text-sm font-semibold text-accent-ink">All counts</Link>
                {c.allowedActions.includes('cancel') && (
                  <Button variant="secondary" onClick={() => setCancelling(true)}>Cancel the count</Button>
                )}
              </>
            }
          />
          {c.status === 'Open' && (
            <CountSheet
              key={c.id}
              count={c}
              onScan={lookup}
              onRecordLine={(skuId, batchId, countedQuantity) => actions.recordLines.mutateAsync([{ skuId, batchId, countedQuantity }])}
              onSubmit={() => actions.submit.mutateAsync()}
            />
          )}
          {c.status === 'Cancelled' && <Banner tone="neutral">This count was cancelled. Nothing was written to stock.</Banner>}
          {hasReport && (
            <QueryBoundary query={variance} loadingLabel="Loading the variance report…">
              {(report) => (
                <CountVariance
                  variance={report}
                  status={c.status}
                  allowedActions={c.allowedActions}
                  onCommit={() => actions.commit.mutateAsync()}
                  onApprove={() => actions.approve.mutateAsync()}
                  onReject={() => actions.reject.mutateAsync()}
                />
              )}
            </QueryBoundary>
          )}
          <ActionDialog
            open={cancelling}
            onOpenChange={setCancelling}
            title={`Cancel ${c.number}`}
            consequence="The count is closed and the lines entered so far are kept on the record but never written to stock."
            confirmLabel="Cancel the count"
            variant="destructive"
            onConfirm={() => actions.cancel.mutateAsync()}
          />
        </div>
      )}
    </QueryBoundary>
  );
}
