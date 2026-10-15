'use client';

import { useState } from 'react';
import { Amount } from '@/components/billing/Amount';
import { Section } from '@/components/billing/PageHeader';
import { Button } from '@/components/ui/button';
import { describeOrderingError } from '@/lib/api/errors';
import type { StaffOrderDetail } from '@/lib/api/staff-orders';
import { formatOrderWhen } from '@/lib/format/orders';
import { useIdempotencyKey } from '@/lib/hooks/useIdempotencyKey';
import { useApproveHeldPayment, useRejectHeldPayment } from '@/lib/hooks/useStaffOrders';
import { OrderReasonDialog } from './OrderReasonDialog';

/**
 * `components/orders/ApprovePaymentPanel.tsx` — HO-ORD-002 §11.9, US-ORD-19 AC2 / FR-BIL-12. A bank
 * transfer above the approval threshold is recorded and held until a second person approves it. The
 * panel shows what was recorded and offers **Approve** and **Reject** (with a note). Who may approve is
 * the server's decision: a recorder approving their own hold gets `SecondApproverMustDiffer` (or
 * Billing's `ApproverMustDiffer`) and the panel shows that message as given.
 */
export function ApprovePaymentPanel({ order }: { order: StaffOrderDetail }) {
  const held = order.heldPayment;
  const approve = useApproveHeldPayment(order.id);
  const reject = useRejectHeldPayment(order.id);
  const key = useIdempotencyKey();
  const [rejectOpen, setRejectOpen] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  if (!held) return null;

  async function onApprove() {
    setMessage(null);
    try {
      await approve.mutateAsync({
        expectedVersion: order.version,
        idempotencyKey: key.keyFor(`${order.id}:${order.version}`),
      });
      key.renew();
    } catch (error) {
      key.settleFailure(error);
      setMessage(describeOrderingError(error));
    }
  }

  return (
    <Section
      title="Payment waiting for approval"
      description="A second staff member must approve this transfer before the order is confirmed."
    >
      <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="text-ink-muted">Amount</dt>
        <dd className="m-0 font-semibold">
          <Amount money={held.amount} />
        </dd>
        <dt className="text-ink-muted">Recorded</dt>
        <dd className="m-0">{formatOrderWhen(held.recordedOn, order.branchTimeZoneId, true)}</dd>
        {order.customerTransferReference && (
          <>
            <dt className="text-ink-muted">Customer’s reference</dt>
            <dd className="m-0 [overflow-wrap:anywhere]">{order.customerTransferReference}</dd>
          </>
        )}
      </dl>
      {message && (
        <p role="alert" className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink">
          {message}
        </p>
      )}
      <div className="flex flex-wrap gap-2">
        <Button onClick={() => void onApprove()} disabled={approve.isPending} aria-busy={approve.isPending}>
          {approve.isPending ? 'Approving…' : 'Approve payment'}
        </Button>
        <Button variant="secondary" onClick={() => setRejectOpen(true)} disabled={approve.isPending}>
          Reject payment
        </Button>
      </div>
      <OrderReasonDialog
        open={rejectOpen}
        onOpenChange={setRejectOpen}
        title="Reject payment"
        consequence={`The recorded transfer for ${order.orderNumber} is rejected and the order goes back to waiting for payment.`}
        confirmLabel="Reject payment"
        reasonLabel="Why is it rejected?"
        onSubmit={(note) => reject.mutateAsync(note)}
      />
    </Section>
  );
}
