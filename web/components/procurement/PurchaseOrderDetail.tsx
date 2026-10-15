'use client';

import * as React from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { ActionDialog, ReasonDialog } from '@/components/inventory/ReasonDialog';
import { Banner, DefinitionList, PageHeading, Panel } from '@/components/inventory/fields';
import { StackedTable } from '@/components/inventory/StackedTable';
import { formatQuantity, formatUnitCost } from '@/lib/format/inventory';
import { formatMoney } from '@/lib/format/money';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import type { PurchaseOrderDetail as PurchaseOrderDetailData, PurchaseOrderLine } from '@/lib/api/procurement-orders';
import { PurchaseOrderStatusBadge, ReceiptStateBadge } from './StatusBadges';

/**
 * `components/procurement/PurchaseOrderDetail.tsx` — HO-IMP-002 §11.2, US-IMP-02 AC2. The header with
 * status, the lines with ordered / accepted / rejected / OUTSTANDING (each with its unit), the
 * receipts (each linking to its goods receipt) and the actions the server allows. Cancel is shown
 * disabled, with the reason in text, when the order has receipts; Close short needs a reason.
 */
export function PurchaseOrderDetail({
  order,
  deliverTo,
  timeZoneId,
  onIssue,
  onCancel,
  onClose,
}: {
  order: PurchaseOrderDetailData;
  deliverTo: string;
  timeZoneId: string;
  onIssue: () => Promise<unknown>;
  onCancel: (reason: string) => Promise<unknown>;
  onClose: (reason: string) => Promise<unknown>;
}) {
  const [dialog, setDialog] = React.useState<'issue' | 'cancel' | 'close' | null>(null);
  const allowed = new Set(order.allowedActions);
  const hasReceipts = order.receipts.length > 0;
  const cancelBlocked = order.status === 'Issued' && !allowed.has('cancel');

  const lineColumns = [
    {
      id: 'item',
      header: 'Item',
      rowHeader: true,
      cell: (l: PurchaseOrderLine) => (
        <span>
          {l.displayName} <span className="font-normal text-ink-muted">— {l.skuCode}</span>
        </span>
      ),
    },
    { id: 'ordered', header: 'Ordered', numeric: true, cell: (l: PurchaseOrderLine) => formatQuantity(l.orderedQuantity, l.stockUnit) },
    { id: 'accepted', header: 'Accepted', numeric: true, cell: (l: PurchaseOrderLine) => formatQuantity(l.acceptedQuantity, l.stockUnit) },
    { id: 'rejected', header: 'Rejected', numeric: true, cell: (l: PurchaseOrderLine) => formatQuantity(l.rejectedQuantity, l.stockUnit) },
    {
      id: 'outstanding',
      header: 'Outstanding',
      numeric: true,
      cell: (l: PurchaseOrderLine) => <strong>{formatQuantity(l.outstandingQuantity, l.stockUnit)}</strong>,
    },
    { id: 'cost', header: 'Unit cost', numeric: true, cell: (l: PurchaseOrderLine) => formatUnitCost(l.unitCost) },
    { id: 'total', header: 'Line total', numeric: true, cell: (l: PurchaseOrderLine) => formatMoney(l.lineTotal) },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title={order.number}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <PurchaseOrderStatusBadge status={order.status} />
            <ReceiptStateBadge state={order.receiptState} />
            <span>{order.supplierName}</span>
          </span>
        }
        actions={
          <>
            {allowed.has('edit') && (
              <Link href={`/staff/procurement/purchase-orders/${order.id}?edit=1`} className={buttonVariants({ variant: 'secondary' })}>
                Edit draft
              </Link>
            )}
            {allowed.has('receive') && (
              <Link href={`/staff/inventory/receipts/new?purchaseOrderId=${order.id}`} className={buttonVariants({ variant: 'secondary' })}>
                Receive goods
              </Link>
            )}
            {allowed.has('issue') && <Button onClick={() => setDialog('issue')}>Issue order</Button>}
            {allowed.has('close') && (
              <Button variant="secondary" onClick={() => setDialog('close')}>
                Close short
              </Button>
            )}
            {(allowed.has('cancel') || cancelBlocked) && (
              <Button
                variant="secondary"
                onClick={() => setDialog('cancel')}
                disabled={cancelBlocked}
                aria-describedby={cancelBlocked ? 'po-cancel-why' : undefined}
              >
                Cancel order
              </Button>
            )}
          </>
        }
      />
      {cancelBlocked && (
        <p id="po-cancel-why" className="text-sm text-ink-muted">
          {hasReceipts
            ? "This order already has receipts, so it can't be cancelled. Close it short instead to stop expecting the rest."
            : "This order can't be cancelled any more."}
        </p>
      )}
      {order.statusReason && <Banner tone="neutral">{order.statusReason}</Banner>}
      <Panel title="Order">
        <DefinitionList
          items={[
            { label: 'Supplier', value: order.supplierName },
            { label: 'Deliver to', value: deliverTo },
            { label: 'Expected delivery', value: formatDateOnly(order.expectedDeliveryOn) },
            { label: 'Currency', value: order.currencyCode },
            { label: 'Issued', value: order.issuedOn ? formatInZone(order.issuedOn, timeZoneId) : 'Not issued yet' },
            { label: 'Notes', value: order.notes ?? '—' },
          ]}
        />
      </Panel>
      <Panel title="Lines">
        <StackedTable
          caption={`Lines of ${order.number}`}
          columns={lineColumns}
          rows={order.lines}
          getKey={(l) => l.lineId}
          footer={[{ label: 'Order total (from the server)', value: formatMoney(order.totalAmount) }]}
        />
        <p className="mt-2 text-xs text-ink-muted">
          Outstanding is what the order still expects: ordered minus accepted. Rejected stock stays outstanding until it is replaced
          or the order is closed short.
        </p>
      </Panel>
      <Panel title="Receipts">
        {order.receipts.length === 0 ? (
          <p className="text-sm text-ink-muted">Nothing has been received against this order yet.</p>
        ) : (
          <ul className="flex flex-col gap-1 text-sm">
            {order.receipts.map((r) => (
              <li key={r.goodsReceiptId}>
                <Link href={`/staff/inventory/receipts/${r.goodsReceiptId}`} className="font-semibold text-accent-ink">
                  {r.number}
                </Link>{' '}
                <span className="text-ink-muted">— {formatInZone(r.receivedOn, timeZoneId)}</span>
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <ActionDialog
        open={dialog === 'issue'}
        onOpenChange={(open) => setDialog(open ? 'issue' : null)}
        title={`Issue ${order.number}`}
        consequence={`This sends ${order.number} to ${order.supplierName} as an order. Its lines can no longer be edited; a mistake is fixed by closing the order short and raising a new one.`}
        confirmLabel="Issue order"
        onConfirm={onIssue}
      />
      <ReasonDialog
        open={dialog === 'cancel'}
        onOpenChange={(open) => setDialog(open ? 'cancel' : null)}
        title={`Cancel ${order.number}`}
        consequence="The order is cancelled and nothing more is expected from the supplier. This cannot be undone."
        reasonLabel="Reason for cancelling"
        confirmLabel="Cancel order"
        variant="destructive"
        onConfirm={onCancel}
      />
      <ReasonDialog
        open={dialog === 'close'}
        onOpenChange={(open) => setDialog(open ? 'close' : null)}
        title={`Close ${order.number} short`}
        consequence="The order is closed with what has been accepted so far; the outstanding quantities are no longer expected. This cannot be undone."
        reasonLabel="Reason for closing short"
        confirmLabel="Close order short"
        variant="destructive"
        onConfirm={onClose}
      />
    </div>
  );
}
