import Link from 'next/link';
import type { BoardingMoney, StayChargeLine, StayInvoiceRef } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { portalInvoiceHref } from '@/lib/routing/portal-links';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/stay/StayCharges.tsx` — HO-BRD-002 §11.3 `StayCharges`, US-BRD-17 AC2: the
 * charge lines exactly as the server sent them (kind, description, signed amount), the deposit
 * received, the balance, and links to the invoices Billing issued. Credits are negative lines. There
 * is **no payment control** (ADR-FE-106): this only shows what is owed and links to the invoice.
 *
 * `invoiceHref` lets the staff workspace link to Billing's staff invoice instead of the portal's.
 */
export function StayCharges({
  charges,
  invoices,
  depositReceived,
  balanceDue,
  billing,
  invoiceHref = portalInvoiceHref,
  className,
}: {
  charges: StayChargeLine[];
  invoices: StayInvoiceRef[];
  depositReceived?: BoardingMoney;
  /** Null/absent when the server sent none: no balance line is drawn (never 0.00). */
  balanceDue?: BoardingMoney | null;
  /** The summary's own totals, under their own labels. */
  billing?: { unbilledTotal: BoardingMoney; invoicedOutstanding: BoardingMoney } | null;
  invoiceHref?: (invoiceId: string) => string;
  className?: string;
}) {
  return (
    <section aria-labelledby="charges-heading" className={cn('flex flex-col gap-3', className)}>
      <h2 id="charges-heading" className="font-display text-lg font-semibold text-ink">
        Charges
      </h2>
      {charges.length === 0 ? (
        <p className="text-sm text-ink-muted">
          No charges yet — they appear once the stay is reserved.
        </p>
      ) : (
        <ul aria-label="Charge lines" className="flex flex-col gap-1.5 text-sm">
          {charges.map((line) => (
            <li key={line.id} className="flex justify-between gap-3">
              <span className="text-ink">
                {line.description}
                {line.invoiceId && <span className="text-ink-muted"> · invoiced</span>}
              </span>
              <span
                className={cn(
                  'whitespace-nowrap font-semibold tabular-nums',
                  line.kind === 'LongStayDiscount' || line.kind === 'EarlyCheckoutCredit'
                    ? 'text-accent-ink'
                    : 'text-ink',
                )}
              >
                {formatMoney(line.amount)}
              </span>
            </li>
          ))}
        </ul>
      )}
      <dl className="grid gap-1 border-t border-border pt-2 text-sm sm:grid-cols-[1fr_auto]">
        {depositReceived && (
          <>
            <dt className="text-ink-muted">Deposit received</dt>
            <dd className="text-right font-semibold tabular-nums text-ink">
              {formatMoney(depositReceived)}
            </dd>
          </>
        )}
        {balanceDue && (
          <>
            <dt className="font-semibold text-ink">Balance due</dt>
            <dd className="text-right font-bold tabular-nums text-ink">
              {formatMoney(balanceDue)}
            </dd>
          </>
        )}
        {billing && (
          <>
            <dt className="text-ink-muted">Not yet invoiced</dt>
            <dd className="text-right font-semibold tabular-nums text-ink">
              {formatMoney(billing.unbilledTotal)}
            </dd>
            <dt className="text-ink-muted">Invoiced, still to pay</dt>
            <dd className="text-right font-semibold tabular-nums text-ink">
              {formatMoney(billing.invoicedOutstanding)}
            </dd>
          </>
        )}
      </dl>
      {invoices.length > 0 && (
        <ul aria-label="Your invoices" className="flex flex-col gap-1 text-sm">
          {invoices.map((invoice) => (
            <li key={invoice.id}>
              <Link
                href={invoiceHref(invoice.id)}
                className="font-semibold text-accent-ink underline"
              >
                Your invoice{invoice.number ? ` ${invoice.number}` : ''}
              </Link>
              {invoice.kind && invoice.total && (
                <span className="text-ink-muted">
                  {' '}
                  ({invoice.kind === 'Cycle' ? 'billing cycle' : 'final'} ·{' '}
                  {formatMoney(invoice.total)})
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
