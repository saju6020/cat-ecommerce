import Link from 'next/link';
import { CircleCheck, Clock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { PaymentRecordResult } from '@/lib/api/billing-staff';
import { moneySign } from '@/lib/money';
import { Amount } from './Amount';
import { InvoiceStatusPill } from './StatusPill';

/**
 * `components/billing/PaymentResultPanel.tsx` — HO-BIL-002 §11.4 + `SplitTenderLoop` (ADR-FE-086).
 * What the server did with the payment: status, **receipt number**, the amounts applied, what is left
 * `unallocated`, and each invoice's new outstanding — all the server's figures. Split tender is a
 * loop, not a multi-row form: while the invoice still has something outstanding the panel offers
 * *"Take another payment"*, pre-filled with the server's new outstanding; at `0.00` it says *"Invoice
 * paid"*; a payment that is pending approval ends the loop with an explanation.
 */
export function PaymentResultPanel({
  result,
  onTakeAnother,
  canLoop,
}: {
  result: PaymentRecordResult;
  onTakeAnother: (outstanding: string, invoiceId: string) => void;
  canLoop: boolean;
}) {
  const pending = result.status === 'PendingApproval';
  const remaining = result.invoiceOutstanding.find((row) => moneySign(row.outstanding.amount) === 'positive');
  const paidInFull = !pending && result.invoiceOutstanding.length > 0 && !remaining;

  return (
    <section
      aria-label="Payment recorded"
      role="status"
      className="flex min-w-0 flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-accent-soft p-4"
    >
      <p className="flex items-center gap-2 text-base font-semibold text-ink">
        {pending ? <Clock size={18} aria-hidden="true" /> : <CircleCheck size={18} aria-hidden="true" />}
        {pending
          ? 'Recorded. An accountant must approve this payment before it counts.'
          : `Recorded${result.receiptNumber ? ` — receipt ${result.receiptNumber}` : ''}.`}
      </p>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="text-ink-muted">Status</dt>
        <dd>{pending ? 'Pending approval' : 'Accepted'}</dd>
        {result.receiptNumber && (
          <>
            <dt className="text-ink-muted">Receipt</dt>
            <dd>
              <Link href={`/staff/billing/payments/${result.paymentId}`} className="font-semibold text-accent-ink hover:underline">
                {result.receiptNumber}
              </Link>
            </dd>
          </>
        )}
        {result.applied.map((a) => (
          <div key={`${a.invoiceId}-${a.amount.amount}`} className="contents">
            <dt className="text-ink-muted">Applied to {a.number ?? 'invoice'}</dt>
            <dd>
              <Amount money={a.amount} />
            </dd>
          </div>
        ))}
        <dt className="text-ink-muted">Unallocated</dt>
        <dd>
          <Amount money={result.unallocated} />
        </dd>
      </dl>
      {result.invoiceOutstanding.length > 0 && (
        <ul className="flex flex-col gap-1 text-sm">
          {result.invoiceOutstanding.map((row) => (
            <li key={row.invoiceId} className="flex flex-wrap items-center gap-2">
              <span className="font-semibold">{row.number}</span>
              <InvoiceStatusPill status={row.status} />
              <span>
                outstanding <Amount money={row.outstanding} />
              </span>
            </li>
          ))}
        </ul>
      )}
      {paidInFull && <p className="text-sm font-semibold text-accent-ink">Invoice paid.</p>}
      {canLoop && remaining && !pending && (
        <div>
          <Button variant="secondary" onClick={() => onTakeAnother(remaining.outstanding.amount, remaining.invoiceId)}>
            Take another payment
          </Button>
        </div>
      )}
    </section>
  );
}
