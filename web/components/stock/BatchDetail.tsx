'use client';

import * as React from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ActionDialog, ReasonDialog } from '@/components/inventory/ReasonDialog';
import { Banner, DefinitionList, PageHeading, Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { StorageChip } from '@/components/inventory/StorageChip';
import { DeclareRecallDialog } from '@/components/recall/DeclareRecallDialog';
import { countryName } from '@/components/inventory/countries';
import { locationKindLabel } from '@/components/inventory/labels';
import { formatDateOrDash, formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import type { DeclareRecallRequest } from '@/lib/api/inventory-recalls';
import type { BatchDetail as BatchDetailData, BatchPosition } from '@/lib/api/inventory-stock';
import { BatchStateBadge, describeBatchState } from './BatchStateBadge';
import { ExpiryBadge } from './ExpiryBadge';

/**
 * `components/stock/BatchDetail.tsx` — HO-IMP-002 §11.4, US-IMP-06/09/11. Provenance, dates, expiry
 * and state in words, the positions per location, the unit cost and its basis (the server leaves the
 * cost fields `null` for a caller without `InventoryReports.View`, and the panel is then simply
 * absent), the recall panel when recalled, and the status history. Actions come ONLY from the
 * server's `allowedActions` — Quarantine (reason), Release, Write off (reason), Recall — each
 * confirmed and each naming the batch and lot. An unusable batch says why and offers nothing that
 * would use it.
 */
export function BatchDetail({
  batch,
  timeZoneId,
  onQuarantine,
  onRelease,
  onWriteOff,
  onDeclareRecall,
}: {
  batch: BatchDetailData;
  timeZoneId: string;
  onQuarantine: (reason: string) => Promise<unknown>;
  onRelease: () => Promise<unknown>;
  onWriteOff: (reason: string) => Promise<unknown>;
  onDeclareRecall: (request: DeclareRecallRequest) => Promise<unknown>;
}) {
  const [dialog, setDialog] = React.useState<'quarantine' | 'release' | 'write-off' | 'recall' | null>(null);
  const allowed = new Set(batch.allowedActions);
  const inQuarantine = batch.positions.some((p) => p.locationKind === 'Quarantine' && p.onHand !== '0');
  const state = describeBatchState({ status: batch.status, isExpired: batch.isExpired, inQuarantineLocation: inQuarantine, statusReason: batch.statusReason });
  const name = `lot ${batch.lotNumber} of ${batch.displayName}`;
  const hasCost = batch.unitCostBase !== null;

  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title={`Lot ${batch.lotNumber}`}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <span>
              {batch.displayName} — {batch.skuCode}
            </span>
            <BatchStateBadge status={batch.status} isExpired={batch.isExpired} inQuarantineLocation={inQuarantine} />
            <ExpiryBadge expiresOn={batch.expiresOn} isExpired={batch.isExpired} daysToExpiry={batch.daysToExpiry} />
          </span>
        }
        actions={
          <>
            {allowed.has('quarantine') && <Button variant="secondary" onClick={() => setDialog('quarantine')}>Quarantine</Button>}
            {allowed.has('release') && <Button variant="secondary" onClick={() => setDialog('release')}>Release</Button>}
            {allowed.has('write-off') && <Button variant="secondary" onClick={() => setDialog('write-off')}>Write off</Button>}
            {allowed.has('recall') && <Button variant="destructive" onClick={() => setDialog('recall')}>Recall…</Button>}
            {allowed.has('withdraw-recall') && (
              <Link href={`/staff/inventory/recalls/${batch.id}`} className="text-sm font-semibold text-accent-ink">
                Open the recall console
              </Link>
            )}
          </>
        }
      />
      {state.why && <Banner tone={state.tone === 'danger' ? 'danger' : 'warning'}>{state.why}</Banner>}

      <Panel title="Provenance and dates">
        <DefinitionList
          items={[
            { label: 'Received', value: `${formatInZone(batch.receivedOn, timeZoneId)} — ${formatQuantity(batch.receivedQuantity, batch.stockUnit)}` },
            {
              label: 'Goods receipt',
              value: (
                <Link href={`/staff/inventory/receipts/${batch.goodsReceiptId}`} className="text-accent-ink">
                  {batch.goodsReceiptNumber ?? 'Open the receipt'}
                </Link>
              ),
            },
            {
              label: 'Consignment',
              value: batch.consignmentId ? (
                <Link href={`/staff/procurement/consignments/${batch.consignmentId}`} className="text-accent-ink">
                  {batch.consignmentReference}
                </Link>
              ) : (
                'None'
              ),
            },
            { label: 'Origin', value: batch.originCountryCode ? countryName(batch.originCountryCode) : '—' },
            { label: 'Manufactured', value: formatDateOrDash(batch.manufacturedOn) },
            { label: 'Expires', value: batch.expiresOn ? formatDateOnly(batch.expiresOn) : 'No expiry date' },
            { label: 'Storage', value: <StorageChip storage={batch.storage} /> },
          ]}
        />
      </Panel>

      <Panel title="Where it is">
        <StackedTable
          caption={`Positions of ${name}`}
          rows={batch.positions}
          getKey={(p) => p.locationId}
          columns={[
            { id: 'loc', header: 'Location', rowHeader: true, cell: (p: BatchPosition) => `${p.locationCode} (${locationKindLabel(p.locationKind)})` },
            { id: 'onHand', header: 'On hand', numeric: true, cell: (p) => formatQuantity(p.onHand, batch.stockUnit) },
            { id: 'reserved', header: 'Reserved', numeric: true, cell: (p) => formatQuantity(p.reserved, batch.stockUnit) },
            { id: 'why', header: 'Note', cell: (p) => (p.locationKind === 'Quarantine' && p.onHand !== '0' ? 'In quarantine location' : '') },
          ]}
        />
      </Panel>

      {hasCost && (
        <Panel title="Cost">
          <DefinitionList
            items={[
              { label: 'Unit cost', value: batch.unitCostBase ? formatUnitCost(batch.unitCostBase) : '—' },
              { label: 'Basis', value: batch.costBasis === 'landed' ? 'Landed (purchase plus allocated import costs)' : 'Purchase' },
              { label: 'Purchase unit cost', value: batch.purchaseUnitCost ? formatUnitCost(batch.purchaseUnitCost) : '—' },
            ]}
          />
        </Panel>
      )}

      {batch.recall && (
        <Panel
          title={`Recall ${batch.recall.recallNumber}`}
          actions={
            <Link href={`/staff/inventory/recalls/${batch.id}`} className="text-sm font-semibold text-accent-ink">
              Open the recall console
            </Link>
          }
        >
          <DefinitionList
            items={[
              { label: 'Declared', value: formatInZone(batch.recall.declaredOn, timeZoneId) },
              { label: 'Reason', value: batch.recall.reason },
              { label: 'Customers messaged', value: `${batch.recall.notifiedRecipientCount} in ${batch.recall.notificationRuns} ${batch.recall.notificationRuns === 1 ? 'run' : 'runs'}` },
            ]}
          />
        </Panel>
      )}

      <Panel title="Status history">
        {batch.statusHistory.length === 0 ? (
          <p className="text-sm text-ink-muted">This batch has not changed status.</p>
        ) : (
          <StackedTable
            caption="Status history"
            rows={batch.statusHistory}
            getKey={(h, i) => `${h.changedOn}-${i}`}
            columns={[
              { id: 'when', header: 'When', rowHeader: true, cell: (h) => formatInZone(h.changedOn, timeZoneId) },
              { id: 'change', header: 'Change', cell: (h) => `${h.from} → ${h.to}` },
              { id: 'reason', header: 'Reason', cell: (h) => h.reason ?? '—' },
            ]}
          />
        )}
      </Panel>

      <ReasonDialog
        open={dialog === 'quarantine'}
        onOpenChange={(open) => setDialog(open ? 'quarantine' : null)}
        title={`Quarantine ${name}`}
        consequence={`Lot ${batch.lotNumber} is held back at once: it can't be sold, dispensed or fed until someone releases it.`}
        reasonLabel="Reason"
        confirmLabel="Quarantine this batch"
        onConfirm={onQuarantine}
      />
      <ActionDialog
        open={dialog === 'release'}
        onOpenChange={(open) => setDialog(open ? 'release' : null)}
        title={`Release ${name}`}
        consequence={`Lot ${batch.lotNumber} becomes available again, subject to its expiry date.`}
        confirmLabel="Release this batch"
        onConfirm={onRelease}
      />
      <ReasonDialog
        open={dialog === 'write-off'}
        onOpenChange={(open) => setDialog(open ? 'write-off' : null)}
        title={`Write off ${name}`}
        consequence={`All remaining stock of lot ${batch.lotNumber} is removed from every location in your branches. This cannot be undone.`}
        reasonLabel="Reason"
        confirmLabel="Write off this batch"
        variant="destructive"
        onConfirm={onWriteOff}
      />
      <DeclareRecallDialog open={dialog === 'recall'} onOpenChange={(open) => setDialog(open ? 'recall' : null)} batch={batch} onDeclare={onDeclareRecall} />
    </div>
  );
}
