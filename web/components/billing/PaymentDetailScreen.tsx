'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Printer } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import type { PaymentDetail, PaymentEntry, PaymentRefundRow } from '@/lib/api/billing-staff';
import { a11yMoney } from '@/lib/money';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { useCustomerInvoices, usePayment, usePaymentMethods } from '@/lib/hooks/billing/useBillingQueries';
import {
  useAllocatePayment,
  useIssueRefund,
  useReleaseEarmark,
  useReversePayment,
} from '@/lib/hooks/billing/useBillingMutations';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { AllocatePaymentDialog } from './AllocatePaymentDialog';
import { Amount } from './Amount';
import { PageHeader, Section, Stat } from './PageHeader';
import { PaymentStatusPill } from './StatusPill';
import { QueryBoundary } from './QueryBoundary';
import { ReasonDialog } from './ReasonDialog';
import { RefundDialog } from './RefundDialog';
import { ResponsiveTable, type Column } from './ResponsiveTable';
import { methodLabel } from './labels';

/**
 * `components/billing/PaymentDetailScreen.tsx` — HO-BIL-002 §11.4 (US-BIL-11, 03). A payment as it is:
 * method, amount, reference, date received, recorder, collector, earmark, approval trail, allocation
 * entries (with release reasons), refunds and status. Actions are **only what the server lists**
 * (`allowedActions`): *Print receipt*, *Allocate*, *Release earmark* (reason), *Refund*, *Reverse*
 * (reason, with the UI-3 consequence). A payment is never edited — a mistake is reversed and
 * re-entered (US-BIL-11 AC3).
 */
export function PaymentDetailScreen({ paymentId }: { paymentId: string }) {
  const { zone } = useActiveBranchZone();
  const { toast } = useToast();
  const payment = usePayment(paymentId);
  const methods = usePaymentMethods();
  const customerId = payment.data?.summary.customerId ?? '';
  const invoices = useCustomerInvoices(customerId);
  const allocate = useAllocatePayment(paymentId);
  const release = useReleaseEarmark(paymentId);
  const reverse = useReversePayment(paymentId);
  const refund = useIssueRefund();
  const [dialog, setDialog] = useState<'allocate' | 'release' | 'refund' | 'reverse' | null>(null);

  const entryColumns: Array<Column<PaymentEntry>> = [
    { id: 'kind', header: 'Entry', primary: true, cell: (e) => (e.kind === 'Allocate' ? 'Applied' : 'Released') },
    { id: 'invoice', header: 'Invoice', cell: (e) => (e.invoiceNumber ? <Link href={`/staff/billing/invoices/${e.invoiceId}`} className="font-semibold text-accent-ink hover:underline">{e.invoiceNumber}</Link> : '—') },
    { id: 'when', header: 'When', cell: (e) => formatInZone(e.occurredOn, zone) },
    { id: 'amount', header: 'Amount', numeric: true, cell: (e) => <Amount money={e.amount} /> },
    { id: 'reason', header: 'Reason', cell: (e) => e.releaseReason ?? '—' },
  ];
  const refundColumns: Array<Column<PaymentRefundRow>> = [
    { id: 'method', header: 'Method', primary: true, cell: (r) => methodLabel(r.method) },
    { id: 'date', header: 'Date', cell: (r) => formatDateOnly(r.businessDate) },
    { id: 'note', header: 'Credit note', cell: (r) => (r.creditNoteId ? <Link href={`/staff/billing/credit-notes/${r.creditNoteId}`} className="font-semibold text-accent-ink hover:underline">{r.creditNoteNumber}</Link> : '—') },
    { id: 'amount', header: 'Amount', numeric: true, cell: (r) => <Amount money={r.amount} /> },
  ];

  return (
    <QueryBoundary query={payment} notFoundTitle="Payment not found" notFoundDescription="This payment may belong to another branch, or it no longer exists." loadingLabel="Loading the payment…">
      {(data: PaymentDetail) => {
        const s = data.summary;
        const can = (action: string) => data.allowedActions.includes(action);
        const applied = [...new Set(data.entries.filter((e) => e.kind === 'Allocate').map((e) => e.invoiceNumber ?? 'an invoice'))];
        const openInvoices = (invoices.data?.items ?? []).filter((i) => i.status === 'Issued' || i.status === 'PartPaid');
        const methodSpec = methods.data?.find((m) => m.name === s.method);
        return (
          <div className="flex flex-col gap-5">
            <PageHeader
              title={s.receiptNumber ? `Receipt ${s.receiptNumber}` : 'Payment awaiting approval'}
              back={{ href: `/staff/billing/customers/${s.customerId}?tab=payments`, label: 'Customer billing account' }}
              description={<span className="flex flex-wrap items-center gap-2"><PaymentStatusPill status={s.status} /> {s.customerName}</span>}
              actions={
                <>
                  {can('receipt') && (
                    <Link href={`/staff/billing/payments/${paymentId}/receipt`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
                      <Printer size={14} aria-hidden="true" />
                      Print receipt
                    </Link>
                  )}
                  {can('allocate') && <Button variant="secondary" size="sm" onClick={() => setDialog('allocate')}>Allocate</Button>}
                  {can('releaseEarmark') && <Button variant="secondary" size="sm" onClick={() => setDialog('release')}>Release earmark</Button>}
                  {can('refund') && <Button variant="secondary" size="sm" onClick={() => setDialog('refund')}>Refund</Button>}
                  {can('reverse') && <Button variant="destructive" size="sm" onClick={() => setDialog('reverse')}>Reverse</Button>}
                  {can('approve') && (
                    <Link href="/staff/billing/approvals" className={buttonVariants({ variant: 'primary', size: 'sm' })}>
                      Review in approvals
                    </Link>
                  )}
                </>
              }
            />
            <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <Stat label="Amount"><Amount money={s.amount} /></Stat>
              <Stat label="Unallocated"><Amount money={s.unallocated} /></Stat>
              <Stat label="Refunded"><Amount money={data.refunded} /></Stat>
              <Stat label="Method">{methodLabel(s.method)}</Stat>
              <Stat label={methodSpec?.referenceLabel ?? 'Reference'}>{s.externalReference ?? '—'}</Stat>
              <Stat label="Date received">{formatDateOnly(s.receivedOn)}</Stat>
              <Stat label="Recorded by" note={formatInZone(s.recordedOn, zone)}>{s.recordedByName}</Stat>
              {data.collector && (
                <Stat label="Collected by">
                  {data.collector.kind === 'Courier' ? `${data.collector.courierName ?? ''} (${data.collector.courierCode ?? ''})` : (data.collector.staffName ?? 'Staff')}
                </Stat>
              )}
              {data.earmark && (
                <Stat label="Earmarked deposit" note={data.earmark.isReleased ? `Released: ${data.earmark.releaseReason ?? ''}` : 'Set aside — not free credit'}>
                  {data.earmark.label}
                </Stat>
              )}
            </dl>
            {data.notes && <p className="text-sm text-ink-muted [overflow-wrap:anywhere]">Notes: {data.notes}</p>}

            <Section title="Approval trail">
              <ol className="flex flex-col gap-1 text-sm">
                <li>Recorded by {data.trail.recordedByName}, {formatInZone(data.trail.recordedOn, zone)}.</li>
                {data.trail.approvedByName && data.trail.approvedOn && <li>Approved by {data.trail.approvedByName}, {formatInZone(data.trail.approvedOn, zone)}.</li>}
                {data.trail.rejectedByName && data.trail.rejectedOn && <li>Rejected by {data.trail.rejectedByName}, {formatInZone(data.trail.rejectedOn, zone)}: {data.trail.rejectionReason}</li>}
                {data.trail.reversedByName && data.trail.reversedOn && <li>Reversed by {data.trail.reversedByName}, {formatInZone(data.trail.reversedOn, zone)}: {data.trail.reversalReason}</li>}
              </ol>
            </Section>
            {data.requestedAllocations.length > 0 && s.status === 'PendingApproval' && (
              <Section title="Requested allocations" description="Applied only if the payment is approved.">
                <ul className="flex flex-col gap-1 text-sm">
                  {data.requestedAllocations.map((r) => (
                    <li key={r.invoiceId}>{r.invoiceNumber} — <Amount money={r.amount} /></li>
                  ))}
                </ul>
              </Section>
            )}
            <Section title="Allocation entries">
              <ResponsiveTable caption="Allocation entries" columns={entryColumns} rows={data.entries} getRowId={(e) => e.entryId} emptyState={{ title: 'Nothing has been applied to an invoice.' }} />
            </Section>
            <Section title="Refunds">
              <ResponsiveTable caption="Refunds" columns={refundColumns} rows={data.refunds} getRowId={(r) => r.refundId} emptyState={{ title: 'No refunds.' }} />
            </Section>

            <AllocatePaymentDialog
              open={dialog === 'allocate'}
              onOpenChange={(open) => !open && setDialog(null)}
              invoices={openInvoices}
              unallocated={s.unallocated}
              onAllocate={(allocations) => allocate.mutateAsync(allocations)}
            />
            <ReasonDialog
              open={dialog === 'release'}
              onOpenChange={(open) => !open && setDialog(null)}
              title="Release the earmark"
              consequence={`“${data.earmark?.label ?? ''}” is no longer set aside. The unallocated ${a11yMoney(s.unallocated)} becomes the customer’s free credit.`}
              confirmLabel="Release earmark"
              pendingLabel="Releasing…"
              variant="primary"
              onSubmit={async (reason) => {
                await release.mutateAsync(reason);
                toast({ title: 'Earmark released' });
              }}
            />
            <ReasonDialog
              open={dialog === 'reverse'}
              onOpenChange={(open) => !open && setDialog(null)}
              title="Reverse this payment"
              consequence={`Reverse receipt ${s.receiptNumber ?? ''}, ${a11yMoney(s.amount)} ${methodLabel(s.method).toLowerCase()}? It will be removed from ${applied.length > 0 ? applied.join(', ') : 'no invoice'} and the day’s cash-up. The receipt stays on record, marked reversed.`}
              confirmLabel="Reverse payment"
              pendingLabel="Reversing…"
              onSubmit={async (reason) => {
                await reverse.mutateAsync(reason);
                toast({ title: 'Payment reversed', description: 'Enter it again correctly if money was received.' });
              }}
            />
            <RefundDialog
              open={dialog === 'refund'}
              onOpenChange={(open) => !open && setDialog(null)}
              paymentId={paymentId}
              allowedMethods={methodSpec?.allowedRefundMethods ?? []}
              methods={methods.data ?? []}
              available={s.unallocated}
              onRefund={(input) => refund.mutateAsync(input)}
            />
          </div>
        );
      }}
    </QueryBoundary>
  );
}
