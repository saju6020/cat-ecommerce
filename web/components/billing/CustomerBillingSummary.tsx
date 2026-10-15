import Link from 'next/link';
import { FilePlus2, HandCoins, Ticket } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { formatDateOnly } from '@/lib/format/time';
import { moneySign } from '@/lib/money';
import type { CustomerBillingSummary as Summary } from '@/lib/api/billing-staff';
import { Amount } from './Amount';
import { Stat } from './PageHeader';

/**
 * `components/billing/CustomerBillingSummary.tsx` — HO-BIL-002 §11.1 (US-BIL-03 AC1). Outstanding,
 * overdue (*"Overdue since 3 Oct"*), free credit, earmarked deposits (label + amount each), unbilled
 * total and loyalty points when enabled — every figure the server's. Actions: **New invoice**,
 * **Record payment**, **Apply credit** (shown only when there is free credit AND something
 * outstanding — read from the server's own figures, not computed).
 */
export function CustomerBillingSummary({
  customerId,
  summary,
  onApplyCredit,
}: {
  customerId: string;
  summary: Summary;
  onApplyCredit: () => void;
}) {
  const overdue = moneySign(summary.overdue.amount) === 'positive';
  const canApplyCredit =
    moneySign(summary.freeCredit.amount) === 'positive' &&
    moneySign(summary.outstanding.amount) === 'positive';

  return (
    <section aria-label="Billing summary" className="flex flex-col gap-4">
      <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Stat label="Outstanding">
          <Amount money={summary.outstanding} />
        </Stat>
        <Stat
          label="Overdue"
          note={
            overdue && summary.oldestDueOn
              ? `Overdue since ${formatDateOnly(summary.oldestDueOn)}`
              : 'Nothing overdue'
          }
        >
          <Amount money={summary.overdue} />
        </Stat>
        <Stat label="Free credit">
          <Amount money={summary.freeCredit} />
        </Stat>
        <Stat label="Unbilled charges">
          <Amount money={summary.unbilledTotal} />
        </Stat>
        <Stat
          label="Earmarked deposits"
          note={summary.earmarkedCredit.length === 0 ? 'None' : undefined}
        >
          {summary.earmarkedCredit.length === 0 ? (
            <span className="text-ink-muted">—</span>
          ) : (
            <ul className="flex flex-col gap-1 text-sm font-normal">
              {summary.earmarkedCredit.map((e) => (
                <li key={e.paymentId} className="flex flex-wrap justify-between gap-2">
                  <span className="[overflow-wrap:anywhere]">{e.label}</span>
                  <Amount money={e.amount} className="font-semibold" />
                </li>
              ))}
            </ul>
          )}
        </Stat>
        {summary.loyaltyEnabled && (
          <Stat label="Loyalty points">
            <span className="tabular-nums">{summary.loyaltyPoints ?? 0}</span>
          </Stat>
        )}
      </dl>
      <div className="flex flex-wrap gap-2">
        <Link
          href={`/staff/billing/invoices/new?customerId=${encodeURIComponent(customerId)}`}
          className={buttonVariants({ variant: 'primary' })}
        >
          <FilePlus2 size={16} aria-hidden="true" />
          New invoice
        </Link>
        <Link
          href={`/staff/billing/payments/new?customerId=${encodeURIComponent(customerId)}`}
          className={buttonVariants({ variant: 'secondary' })}
        >
          <HandCoins size={16} aria-hidden="true" />
          Record payment
        </Link>
        {canApplyCredit && (
          <Button variant="secondary" onClick={onApplyCredit}>
            <Ticket size={16} aria-hidden="true" />
            Apply credit
          </Button>
        )}
      </div>
    </section>
  );
}
