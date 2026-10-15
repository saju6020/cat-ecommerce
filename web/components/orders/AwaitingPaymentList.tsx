'use client';

import Link from 'next/link';
import { useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { Amount, AmountCell } from '@/components/billing/Amount';
import { PageHeader, Section } from '@/components/billing/PageHeader';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { AwaitingPaymentOrder } from '@/lib/api/staff-orders';
import { formatOrderWhen, formatTimeLeft } from '@/lib/format/orders';
import { useServerNow } from '@/lib/hooks/useServerNow';
import { useAwaitingPayment } from '@/lib/hooks/useStaffOrders';
import { ConfirmTransferDialog, type TransferTarget } from './ConfirmTransferDialog';
import { StaffOrdersTabs } from './StaffOrdersTabs';

/**
 * `components/orders/AwaitingPaymentList.tsx` — ⭐ HO-ORD-002 §11.9, US-ORD-19 AC2. Bank-transfer orders
 * that wait for money: number, customer, amount due, the deadline with the time left (on the server
 * clock), and the reference the customer reported. Orders whose transfer is recorded and held for a
 * second person are listed apart as *Awaiting approval* (they are approved on the order). **Confirm
 * transfer** opens the dialog, which restates the amount before sending. Refreshes every 30 s; an open
 * dialog keeps its values because the target is held in state, not read from the list.
 */
function DueBy({ order }: { order: AwaitingPaymentOrder }) {
  const now = useServerNow(30_000);
  if (!order.paymentDueBy) return <span>No deadline</span>;
  const remaining = Date.parse(order.paymentDueBy) - now;
  return (
    <span>
      {formatOrderWhen(order.paymentDueBy, order.branchTimeZoneId)}{' '}
      <span className="text-ink-muted">({remaining > 0 ? `${formatTimeLeft(remaining)} left` : 'overdue'})</span>
    </span>
  );
}

export function AwaitingPaymentList() {
  const query = useAwaitingPayment();
  const [target, setTarget] = useState<TransferTarget | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  const rows = query.data ?? [];
  const waiting = rows.filter((o) => !o.heldPayment);
  const held = rows.filter((o) => o.heldPayment);

  const columns: Array<Column<AwaitingPaymentOrder>> = [
    { id: 'number', header: 'Order', primary: true, cell: (o) => o.orderNumber },
    { id: 'customer', header: 'Customer', sortValue: (o) => o.customerName, cell: (o) => o.customerName },
    { id: 'amount', header: 'Amount due', numeric: true, cell: (o) => <AmountCell money={o.amount} /> },
    { id: 'due', header: 'Pay by', sortValue: (o) => o.paymentDueBy ?? '~', cell: (o) => <DueBy order={o} /> },
    { id: 'reference', header: 'Customer’s reference', cell: (o) => o.customerTransferReference ?? 'Not reported yet' },
    {
      id: 'action',
      header: 'Action',
      cell: (o) => (
        <Button
          size="sm"
          className="h-auto whitespace-normal py-1.5 text-left"
          onClick={() => {
            setNotice(null);
            setTarget({
              orderId: o.id,
              orderNumber: o.orderNumber,
              version: o.version,
              amount: o.amount,
              zone: o.branchTimeZoneId,
              customerReference: o.customerTransferReference,
            });
          }}
        >
          Confirm transfer<span className="sr-only"> for {o.orderNumber}</span>
        </Button>
      ),
    },
  ];
  const heldColumns: Array<Column<AwaitingPaymentOrder>> = [
    { id: 'number', header: 'Order', primary: true, cell: (o) => o.orderNumber },
    { id: 'customer', header: 'Customer', cell: (o) => o.customerName },
    { id: 'amount', header: 'Amount', numeric: true, cell: (o) => (o.heldPayment ? <Amount money={o.heldPayment.amount} /> : null) },
    { id: 'recorded', header: 'Recorded', cell: (o) => (o.heldPayment ? formatOrderWhen(o.heldPayment.recordedOn, o.branchTimeZoneId) : '') },
    { id: 'status', header: 'Status', cell: () => 'Awaiting approval' },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Awaiting payment" description="Bank transfers to confirm against the bank statement. The list refreshes every 30 seconds." />
      <StaffOrdersTabs />
      {notice && (
        <p role="status" className="rounded-[var(--radius-sm)] bg-accent-soft p-3 text-sm font-semibold text-accent-ink">
          {notice}
        </p>
      )}
      <Section title="Waiting for the transfer">
        <ResponsiveTable
          caption="Orders waiting for a bank transfer"
          columns={columns}
          rows={waiting}
          getRowId={(o) => o.id}
          rowHref={(o) => `/staff/orders/${o.id}`}
          isLoading={query.isLoading}
          isError={query.isError}
          onRetry={() => void query.refetch()}
          emptyState={{ title: 'Nothing is waiting for a transfer.', description: 'Orders paid by bank transfer appear here until the money is confirmed.' }}
        />
      </Section>
      {held.length > 0 && (
        <Section title="Awaiting approval" description="Recorded, but a second staff member must approve each one on its order page.">
          <ResponsiveTable
            caption="Transfers awaiting approval"
            columns={heldColumns}
            rows={held}
            getRowId={(o) => o.id}
            rowHref={(o) => `/staff/orders/${o.id}`}
            emptyState={{ title: 'Nothing awaits approval.' }}
          />
          <p className="text-sm text-ink-muted">
            Open an order to <Link href={`/staff/orders/${held[0]!.id}`} className="text-accent-ink underline">approve or reject</Link> its payment.
          </p>
        </Section>
      )}
      {target && (
        <ConfirmTransferDialog
          target={target}
          open
          onOpenChange={(next) => !next && setTarget(null)}
          onDone={(outcome) =>
            setNotice(
              outcome.awaitingApproval
                ? 'Recorded. A second staff member must approve it before the order is confirmed.'
                : 'Transfer confirmed. The order is now confirmed.',
            )
          }
        />
      )}
    </div>
  );
}
