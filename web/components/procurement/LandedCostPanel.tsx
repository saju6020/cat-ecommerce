'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { ActionDialog } from '@/components/inventory/ReasonDialog';
import { Banner, Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import { formatInZone } from '@/lib/format/time';
import type { LandedCost, LandedCostLine } from '@/lib/api/procurement-consignments';

/**
 * `components/procurement/LandedCostPanel.tsx` — HO-IMP-002 §11.2, US-IMP-05. **Allocate landed
 * cost** (confirmed: *"This recalculates the landed unit cost of {n} batches."*), then one row per
 * batch: quantity, purchase cost, allocated cost and the landed UNIT cost to four decimals (strings,
 * straight from the server). A **stale** banner when the costs or the batches changed since the last
 * allocation. The basis is one sentence: costs are shared by quantity received.
 */
export function LandedCostPanel({
  landed,
  unitsByBatch,
  batchCount,
  canAllocate,
  timeZoneId,
  onAllocate,
}: {
  landed: LandedCost;
  /** The stock unit of each batch (the landed-cost lines carry a quantity but no unit). */
  unitsByBatch: Record<string, string>;
  batchCount: number;
  canAllocate: boolean;
  timeZoneId: string;
  onAllocate: () => Promise<unknown>;
}) {
  const [open, setOpen] = React.useState(false);
  const allocated = landed.allocatedOn !== null;

  return (
    <Panel
      title="Landed cost"
      actions={
        canAllocate && (
          <Button onClick={() => setOpen(true)} disabled={batchCount === 0}>
            {allocated ? 'Allocate again' : 'Allocate landed cost'}
          </Button>
        )
      }
    >
      <p className="mb-3 text-sm text-ink-muted">
        The consignment&rsquo;s costs are shared across the batches received on it, in proportion to the quantity received of each.
      </p>
      {landed.isStale && (
        <Banner tone="warning" className="mb-3">
          Costs or batches changed since the last allocation. Allocate again to bring the landed unit costs up to date.
        </Banner>
      )}
      {batchCount === 0 && (
        <p className="text-sm text-ink-muted">Nothing has been received on this consignment yet, so there is nothing to allocate costs across.</p>
      )}
      {!allocated && batchCount > 0 && <p className="text-sm text-ink-muted">Landed cost has not been allocated yet.</p>}
      {allocated && (
        <>
          <p className="mb-2 text-sm text-ink-muted">Allocated {formatInZone(landed.allocatedOn!, timeZoneId)}.</p>
          <StackedTable
            caption="Landed cost per batch"
            columns={[
              { id: 'batch', header: 'Batch', rowHeader: true, cell: (l: LandedCostLine) => `${l.skuCode ?? 'Batch'} · lot ${l.lotNumber ?? '—'}` },
              { id: 'qty', header: 'Quantity', numeric: true, cell: (l) => formatQuantity(l.quantity, unitsByBatch[l.batchId] ?? '') },
              { id: 'purchase', header: 'Purchase cost', numeric: true, cell: (l) => formatMoney(l.purchaseCostBase) },
              { id: 'allocated', header: 'Allocated cost', numeric: true, cell: (l) => formatMoney(l.allocatedCostBase) },
              { id: 'landed', header: 'Landed unit cost', numeric: true, cell: (l) => <strong>{formatUnitCost(l.landedUnitCostBase)}</strong> },
            ]}
            rows={landed.lines}
            getKey={(l) => l.batchId}
            footer={landed.totalCostsBase ? [{ label: 'Total costs allocated', value: formatMoney(landed.totalCostsBase) }] : undefined}
          />
        </>
      )}
      <ActionDialog
        open={open}
        onOpenChange={setOpen}
        title="Allocate landed cost"
        consequence={`This recalculates the landed unit cost of ${batchCount} ${batchCount === 1 ? 'batch' : 'batches'}.`}
        confirmLabel="Allocate landed cost"
        onConfirm={onAllocate}
      />
    </Panel>
  );
}
