'use client';

import { use } from 'react';
import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import { Panel } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { StackedTable } from '@/components/inventory/StackedTable';
import { BatchStateBadge } from '@/components/stock/BatchStateBadge';
import { formatDateOrDash, formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import type { ConsignmentBatch } from '@/lib/api/procurement-consignments';
import { useConsignment, useInspectionPack } from '@/lib/hooks/useProcurement';

/** `/staff/procurement/consignments/[consignmentId]/batches` — what arrived (US-IMP-04, US-IMP-05):
 *  lot, expiry, quantity received, status, the receipt, and the landed unit cost where allocated. */
export default function ConsignmentBatchesPage({ params }: { params: Promise<{ consignmentId: string }> }) {
  const { consignmentId } = use(params);
  const pack = useInspectionPack(consignmentId);
  const consignment = useConsignment(consignmentId);
  const landedByBatch = new Map((consignment.data?.landedCost.lines ?? []).map((l) => [l.batchId, l]));
  return (
    <QueryBoundary query={pack}>
      {(data) =>
        data.batches.length === 0 ? (
          <EmptyState title="Nothing has arrived yet" description="Batches appear here once goods are received against this consignment." />
        ) : (
          <Panel title="Batches received">
            <StackedTable
              caption="Batches received on this consignment"
              rows={data.batches}
              getKey={(b) => b.batchId}
              columns={[
                {
                  id: 'batch',
                  header: 'Batch',
                  rowHeader: true,
                  cell: (b: ConsignmentBatch) => (
                    <Link href={`/staff/inventory/batches/${b.batchId}`} className="text-accent-ink">
                      {b.displayName} · lot {b.lotNumber}
                    </Link>
                  ),
                },
                { id: 'expiry', header: 'Expiry', cell: (b) => formatDateOrDash(b.expiresOn, 'No expiry date') },
                { id: 'qty', header: 'Received', numeric: true, cell: (b) => formatQuantity(b.receivedQuantity, b.stockUnit) },
                { id: 'receipt', header: 'Receipt', cell: (b) => b.goodsReceiptNumber },
                { id: 'status', header: 'Status', cell: (b) => <BatchStateBadge status={b.status as 'Available'} /> },
                {
                  id: 'cost',
                  header: 'Unit cost',
                  numeric: true,
                  cell: (b) => {
                    const line = landedByBatch.get(b.batchId);
                    return line ? `${formatUnitCost(line.landedUnitCostBase)} (landed)` : 'Not allocated';
                  },
                },
              ]}
            />
          </Panel>
        )
      }
    </QueryBoundary>
  );
}
