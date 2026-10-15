'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Button, buttonVariants } from '@/components/ui/button';
import { PageHeader } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { RefundDialog } from '@/components/refunds/RefundDialog';
import { describeOrderingError } from '@/lib/api/errors';
import type { StaffOrderAction, StaffOrderDetail as OrderDetail, StaffOrderLine } from '@/lib/api/staff-orders';
import { formatOrderWhen, orderActionLabel } from '@/lib/format/orders';
import {
  useCancelStaffOrder,
  useMarkReadyForCollection,
  useStaffOrder,
  useStartPacking,
} from '@/lib/hooks/useStaffOrders';
import { ApprovePaymentPanel } from './ApprovePaymentPanel';
import { ConfirmTransferDialog } from './ConfirmTransferDialog';
import { DeliveryFailureDialog } from './DeliveryFailureDialog';
import { DispatchDialog, TrackingDialog } from './DispatchDialog';
import { OrderReasonDialog } from './OrderReasonDialog';
import { OrderStatusBadge } from './OrderStatusBadge';
import { OrderSummary } from './OrderSummary';
import { RecordDeliveryDialog } from './RecordDeliveryDialog';
import { ReallocateLineDialog } from './ReallocateLineDialog';
import {
  CustomerFulfilmentPanel,
  PaymentsPanel,
  ShipmentPanel,
  StaffHistoryPanel,
  StaffLinesPanel,
} from './StaffOrderPanels';

/**
 * `components/orders/StaffOrderDetail.tsx` — ⭐ HO-ORD-002 §11.8, ADR-FE-093. The action bar is rendered
 * **only from the server's `allowedActions`** (plus the held-payment panel when a transfer waits for a
 * second person); no action is invented from the status. The order refetches every 30 s (UI-5); the
 * dialogs live here, not under the buttons, so a refetch that removes an action never closes a dialog
 * someone is typing in. A `403` shows the permission-denied state.
 */
type Dialog = 'transfer' | 'cancel' | 'dispatch' | 'tracking' | 'delivery' | 'failure' | 'refund' | 'reallocate';

const PRIMARY: StaffOrderAction[] = ['ConfirmPayment', 'StartPacking', 'Dispatch', 'MarkReadyForCollection', 'RecordDelivery', 'RecordDeliveryFailure', 'Refund'];

function Detail({ order }: { order: OrderDetail }) {
  const router = useRouter();
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [reallocLine, setReallocLine] = useState<string | undefined>();
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const startPacking = useStartPacking();
  const ready = useMarkReadyForCollection(order.id);
  const cancel = useCancelStaffOrder(order.id);
  const open = (next: Dialog | null) => setDialog(next);
  const allowed = new Set<string>(order.allowedActions);
  const fulfilmentWork = allowed.has('StartPacking') || allowed.has('Dispatch') || allowed.has('MarkReadyForCollection');

  async function direct(run: () => Promise<unknown>) {
    setError(null);
    try {
      await run();
    } catch (failure) {
      setError(describeOrderingError(failure));
    }
  }

  function perform(action: StaffOrderAction) {
    setNotice(null);
    switch (action) {
      case 'ConfirmPayment': return open('transfer');
      case 'StartPacking': return void direct(() => startPacking.mutateAsync(order.id));
      case 'Dispatch': return open('dispatch');
      case 'MarkReadyForCollection': return void direct(() => ready.mutateAsync());
      case 'RecordDelivery': return open('delivery');
      case 'RecordDeliveryFailure': return open('failure');
      case 'Refund': return open('refund');
      default: return undefined;
    }
  }

  const bar = PRIMARY.filter((a) => allowed.has(a) && !(a === 'ConfirmPayment' && order.heldPayment));
  const busy = startPacking.isPending || ready.isPending;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        back={{ href: '/staff/orders', label: 'Orders' }}
        title={`Order ${order.orderNumber}`}
        description={`Placed ${formatOrderWhen(order.placedOn, order.branchTimeZoneId, true)} · ${order.channel === 'Counter' ? 'Counter sale' : 'Online'}`}
        actions={
          <>
            <OrderStatusBadge status={order.status} />
            {order.fulfilmentMethod !== 'Counter' && (
              <Link href={`/staff/orders/${order.id}/packing-slip`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
                Packing slip
              </Link>
            )}
          </>
        }
      />
      {(bar.length > 0 || allowed.has('Cancel')) && (
        <div role="group" aria-label="Order actions" className="flex flex-wrap gap-2">
          {bar.map((action) => (
            <Button key={action} onClick={() => perform(action)} disabled={busy} variant={action === 'Refund' ? 'secondary' : 'primary'}>
              {orderActionLabel(action, order.fulfilmentMethod)}
            </Button>
          ))}
          {allowed.has('Cancel') && (
            <Button variant="destructive" onClick={() => open('cancel')} disabled={busy}>
              Cancel order
            </Button>
          )}
        </div>
      )}
      {error && <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">{error}</p>}
      {notice && <p role="status" className="rounded-[var(--radius-sm)] bg-accent-soft p-3 text-sm font-semibold text-accent-ink">{notice}</p>}
      <ApprovePaymentPanel order={order} />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <div className="flex min-w-0 flex-col gap-5">
          <StaffLinesPanel
            order={order}
            onReallocate={fulfilmentWork ? (line: StaffOrderLine) => { setReallocLine(line.lineId); open('reallocate'); } : undefined}
          />
          <ShipmentPanel order={order} onEditTracking={() => open('tracking')} />
          <StaffHistoryPanel order={order} />
        </div>
        <div className="flex min-w-0 flex-col gap-5">
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
            <OrderSummary totals={order.totals} discountCode={order.discountCode} serviceLevel={order.fulfilment.serviceLevel} fulfilment={order.fulfilmentMethod} />
          </div>
          <CustomerFulfilmentPanel order={order} />
          <PaymentsPanel order={order} />
        </div>
      </div>

      <ConfirmTransferDialog
        target={{
          orderId: order.id,
          orderNumber: order.orderNumber,
          version: order.version,
          amount: order.totals.grandTotal,
          zone: order.branchTimeZoneId,
          customerReference: order.customerTransferReference,
        }}
        open={dialog === 'transfer'}
        onOpenChange={(next) => open(next ? 'transfer' : null)}
        onDone={(outcome) =>
          setNotice(outcome.awaitingApproval
            ? 'Recorded. A second staff member must approve it before the order is confirmed.'
            : 'Transfer confirmed. The order is now confirmed.')
        }
      />
      <OrderReasonDialog
        open={dialog === 'cancel'}
        onOpenChange={(next) => open(next ? 'cancel' : null)}
        title={`Cancel order ${order.orderNumber}`}
        consequence="The reserved items are released back to stock. Anything the customer has already paid becomes a refund due."
        confirmLabel="Cancel order"
        reasonLabel="Why is it being cancelled?"
        onSubmit={(note) => cancel.mutateAsync(note)}
      />
      <DispatchDialog
        orderId={order.id}
        open={dialog === 'dispatch'}
        onOpenChange={(next) => open(next ? 'dispatch' : null)}
        onReallocate={() => { setReallocLine(undefined); open('reallocate'); }}
      />
      <TrackingDialog orderId={order.id} current={order.shipment?.trackingNumber ?? null} open={dialog === 'tracking'} onOpenChange={(next) => open(next ? 'tracking' : null)} />
      <RecordDeliveryDialog order={order} open={dialog === 'delivery'} onOpenChange={(next) => open(next ? 'delivery' : null)} />
      <DeliveryFailureDialog
        orderId={order.id}
        fulfilmentMethod={order.fulfilmentMethod}
        open={dialog === 'failure'}
        onOpenChange={(next) => open(next ? 'failure' : null)}
        onReturnOpened={(id) => router.push(`/staff/returns/${id}`)}
      />
      <ReallocateLineDialog orderId={order.id} lines={order.lines} initialLineId={reallocLine} open={dialog === 'reallocate'} onOpenChange={(next) => open(next ? 'reallocate' : null)} />
      <RefundDialog orderId={order.id} orderNumber={order.orderNumber} open={dialog === 'refund'} onOpenChange={(next) => open(next ? 'refund' : null)} />
    </div>
  );
}

export function StaffOrderDetail({ orderId }: { orderId: string }) {
  const query = useStaffOrder(orderId);
  return (
    <QueryBoundary query={query} notFoundTitle="Order not found" notFoundDescription="This order may belong to another branch." loadingLabel="Loading the order…">
      {(order) => <Detail order={order} />}
    </QueryBoundary>
  );
}
