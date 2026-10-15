'use client';

import Link from 'next/link';
import { Banner, DefinitionList, PageHeading, Panel } from './fields';
import { StackedTable } from './StackedTable';
import { StorageChip } from './StorageChip';
import { BatchStateBadge } from '@/components/stock/BatchStateBadge';
import { rejectionReasonLabel } from './labels';
import { formatDateOrDash, formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import type { BatchStatus } from '@/lib/api/inventory-stock';
import type { GoodsReceiptDetail, GoodsReceiptLine } from '@/lib/api/inventory-receipts';

/**
 * `components/inventory/ReceiptDetail.tsx` — HO-IMP-002 §11.3 item 6. A posted receipt, read-only:
 * header, lines (with the storage requirement and rejection), the batches it created (links) and
 * the ledger entries it wrote (a link to the ledger filtered to this receipt). There is no edit
 * control — a mistake is corrected by an adjustment.
 */
export function ReceiptDetail({ receipt, timeZoneId }: { receipt: GoodsReceiptDetail; timeZoneId: string }) {
  const costsHidden = receipt.lines.every((l) => l.unitCost === null);
  const created = receipt.lines.filter((l) => l.batchId);
  const day = receipt.receivedOn.slice(0, 10);
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title={receipt.number}
        description={`Received ${formatInZone(receipt.receivedOn, timeZoneId)}`}
        actions={
          <Link
            href={`/staff/inventory/ledger?from=${day}&to=${day}&referenceKind=GoodsReceipt&referenceId=${receipt.id}`}
            className="text-sm font-semibold text-accent-ink"
          >
            See its ledger entries
          </Link>
        }
      />
      <Banner tone="neutral">A posted receipt is permanent. If something was wrong, record an adjustment.</Banner>
      <Panel title="Receipt">
        <DefinitionList
          items={[
            { label: 'Supplier', value: receipt.supplierName ?? '—' },
            {
              label: 'Purchase order',
              value: receipt.purchaseOrderId ? (
                <Link href={`/staff/procurement/purchase-orders/${receipt.purchaseOrderId}`} className="text-accent-ink">
                  {receipt.purchaseOrderNumber}
                </Link>
              ) : (
                'None — received directly'
              ),
            },
            {
              label: 'Consignment',
              value: receipt.consignmentId ? (
                <Link href={`/staff/procurement/consignments/${receipt.consignmentId}`} className="text-accent-ink">
                  {receipt.consignmentReference}
                </Link>
              ) : (
                'None'
              ),
            },
            { label: 'Delivery reference', value: receipt.deliveryReference ?? '—' },
          ]}
        />
      </Panel>
      <Panel title="Lines">
        <StackedTable
          caption={`Lines of ${receipt.number}`}
          rows={receipt.lines}
          getKey={(l) => l.lineId}
          columns={[
            {
              id: 'item',
              header: 'Item',
              rowHeader: true,
              cell: (l: GoodsReceiptLine) => (
                <span>
                  {l.displayName} <span className="font-normal text-ink-muted">— {l.skuCode}</span>
                  <span className="mt-1 block"><StorageChip storage={l.storage} /></span>
                </span>
              ),
            },
            { id: 'lot', header: 'Lot', cell: (l) => l.lotNumber },
            { id: 'expiry', header: 'Expiry', cell: (l) => formatDateOrDash(l.expiresOn, 'No expiry date') },
            { id: 'accepted', header: 'Accepted', numeric: true, cell: (l) => formatQuantity(l.quantityAccepted, l.stockUnit) },
            {
              id: 'rejected',
              header: 'Rejected',
              numeric: true,
              cell: (l) =>
                l.quantityRejected === '0'
                  ? formatQuantity('0', l.stockUnit)
                  : `${formatQuantity(l.quantityRejected, l.stockUnit)} — ${rejectionReasonLabel(l.rejectionReason)}${l.rejectionNote ? ` (${l.rejectionNote})` : ''}`,
            },
            { id: 'location', header: 'Location', cell: (l) => l.locationCode ?? '—' },
            ...(costsHidden ? [] : [{ id: 'cost', header: 'Unit cost', numeric: true, cell: (l: GoodsReceiptLine) => (l.unitCost ? formatUnitCost(l.unitCost) : '—') }]),
          ]}
        />
      </Panel>
      <Panel title="Batches created">
        {created.length === 0 ? (
          <p className="text-sm text-ink-muted">Nothing was accepted, so no batch was created.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {created.map((l) => (
              <li key={l.lineId} className="flex flex-wrap items-center gap-2">
                <Link href={`/staff/inventory/batches/${l.batchId}`} className="font-semibold text-accent-ink">
                  {l.displayName} · lot {l.lotNumber}
                </Link>
                {l.batchStatus && <BatchStateBadge status={l.batchStatus as BatchStatus} />}
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  );
}
