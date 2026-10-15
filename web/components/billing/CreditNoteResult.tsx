'use client';

import { useState } from 'react';
import Link from 'next/link';
import { CircleCheck } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import type { InvoiceDetail, IssuedCreditNote, IssueRefundRequest } from '@/lib/api/billing-staff';
import { moneySign } from '@/lib/money';
import { useCreditNote, usePayment, usePaymentMethods } from '@/lib/hooks/billing/useBillingQueries';
import { Amount } from './Amount';
import { SelectField } from './Fields';
import { RefundDialog } from './RefundDialog';
import { Section } from './PageHeader';

/**
 * `components/billing/CreditNoteResult.tsx` — HO-BIL-002 §11.3 step 3 (US-BIL-05 AC2, FR-ORD-17). What
 * the server did: the credit note's number and total, and — when the credit left the invoice
 * over-paid — the amount **released to customer credit**. The choice is *"Keep as customer credit"*
 * (the default; nothing to do) or **Refund now**, which opens the refund dialog linked to this credit
 * note, for the payment you pick. From here the invoice can also be re-issued, corrected.
 */
export function CreditNoteResult({
  invoice,
  issued,
  onRefund,
  onCorrectedReissue,
  reissuing,
}: {
  invoice: InvoiceDetail;
  issued: IssuedCreditNote;
  onRefund: (input: { request: IssueRefundRequest; idempotencyKey: string }) => Promise<{ refundId: string }>;
  onCorrectedReissue: () => void;
  reissuing: boolean;
}) {
  const note = useCreditNote(issued.creditNoteId);
  const methods = usePaymentMethods();
  const released = moneySign(issued.releasedToCustomerCredit.amount) === 'positive';
  const [refundOpen, setRefundOpen] = useState(false);
  const [paymentId, setPaymentId] = useState(invoice.payments[0]?.paymentId ?? '');
  const [refunded, setRefunded] = useState(false);
  const payment = usePayment(released ? paymentId : '');
  const methodSpec = methods.data?.find((m) => m.name === payment.data?.summary.method);

  return (
    <div className="flex flex-col gap-4">
      <p role="status" className="flex items-center gap-2 text-base font-semibold text-ink">
        <CircleCheck size={18} aria-hidden="true" />
        Credit note {issued.number} issued.
      </p>
      <Section title="Result">
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
          <dt className="text-ink-muted">Credit note</dt>
          <dd>
            <Link href={`/staff/billing/credit-notes/${issued.creditNoteId}`} className="font-semibold text-accent-ink hover:underline">
              {issued.number}
            </Link>
          </dd>
          {note.data && (
            <>
              <dt className="text-ink-muted">Total credited</dt>
              <dd><Amount money={note.data.total} /></dd>
            </>
          )}
          <dt className="text-ink-muted">Released to customer credit</dt>
          <dd><Amount money={issued.releasedToCustomerCredit} /></dd>
        </dl>
        {released && !refunded && (
          <div className="flex flex-col gap-3">
            <p className="text-sm text-ink">
              The invoice was already paid, so this amount is now the customer’s credit. Keep it as credit
              (the default — they can use it on another invoice), or refund it now.
            </p>
            {invoice.payments.length > 1 && (
              <SelectField id="refund-payment" label="Refund from payment" wrapperClassName="max-w-xs" value={paymentId} onChange={(e) => setPaymentId(e.target.value)}>
                {invoice.payments.map((p) => (
                  <option key={p.paymentId} value={p.paymentId}>{p.receiptNumber} — {p.method}</option>
                ))}
              </SelectField>
            )}
            <div className="flex flex-wrap gap-2">
              <Button variant="secondary" disabled>Keep as customer credit (selected)</Button>
              <Button onClick={() => setRefundOpen(true)}>Refund now</Button>
            </div>
          </div>
        )}
        {refunded && <p role="status" className="text-sm font-semibold text-accent-ink">Refund recorded against this credit note.</p>}
      </Section>
      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" disabled={reissuing} onClick={onCorrectedReissue}>
          Corrected re-issue
        </Button>
        <Link href={`/staff/billing/invoices/${invoice.invoiceId}`} className={buttonVariants({ variant: 'ghost' })}>
          Back to the invoice
        </Link>
      </div>
      {payment.data && (
        <RefundDialog
          open={refundOpen}
          onOpenChange={setRefundOpen}
          paymentId={paymentId}
          allowedMethods={methodSpec?.allowedRefundMethods ?? []}
          methods={methods.data ?? []}
          available={payment.data.summary.unallocated}
          creditNoteId={issued.creditNoteId}
          defaultAmount={issued.releasedToCustomerCredit.amount}
          onRefund={onRefund}
          onDone={() => setRefunded(true)}
        />
      )}
    </div>
  );
}
