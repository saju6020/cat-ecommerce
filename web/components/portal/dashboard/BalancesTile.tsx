'use client';

import Link from 'next/link';
import { TriangleAlert } from 'lucide-react';
import type { PortalBalances, PortalMoney } from '@/lib/api/portal';
import { usePortalClinicContacts } from '@/lib/hooks/usePortalDashboard';
import { formatMoney } from '@/lib/format/money';
import { formatDateOnly } from '@/lib/format/time';
import { portalInvoiceHref } from '@/lib/routing/portal-links';
import { invoiceStatusLabel } from './labels';
import { StatusBadge } from './StatusBadge';
import { TileEmpty, TileRow } from './DashboardSection';

/**
 * Balances are guidance, not a payment surface (SRS §1.2, ADR-FE-122): what is owed per currency,
 * how to pay offline (the backend's own text), the clinics' phone numbers, and links to the
 * invoices. There is no pay control, and amounts in different currencies are never added.
 */

/** Billing's open-invoice scan stops at 500 issued invoices (HO-POR-001 §1 Amended (7)), so a
 *  count of 500 can only mean "at least 500". */
const OPEN_INVOICE_SCAN_CAP = 500;

function overdueFor(overdue: PortalMoney[], currencyCode: string): PortalMoney | undefined {
  return overdue.find((m) => m.currencyCode === currencyCode && Number(m.amount) > 0);
}

function ClinicContacts() {
  const { data } = usePortalClinicContacts();
  const withPhone = (data ?? []).filter((branch) => branch.phoneNumber);
  if (withPhone.length === 0) return null;
  return (
    <ul className="mt-2 flex flex-wrap gap-x-5 gap-y-1.5">
      {withPhone.map((branch) => (
        <li key={branch.id}>
          <b>{branch.name}</b> · {branch.phoneNumber}
        </li>
      ))}
    </ul>
  );
}

function openInvoicesLabel(count: number, shown: number): string {
  if (count >= OPEN_INVOICE_SCAN_CAP) return `${OPEN_INVOICE_SCAN_CAP} or more open invoices`;
  const noun = count === 1 ? 'open invoice' : 'open invoices';
  return shown < count
    ? `${count} ${noun} · showing the oldest ${shown}`
    : `${count} ${noun} · oldest due first`;
}

export function BalancesTile({ data }: { data: PortalBalances }) {
  if (!data.hasOutstanding) {
    return (
      <>
        <TileEmpty>Nothing to pay right now.</TileEmpty>
        <p className="pb-2 text-[13px] text-ink-muted">{data.howToPay}</p>
      </>
    );
  }
  return (
    <div className="grid gap-x-7 gap-y-1 [grid-template-columns:repeat(auto-fit,minmax(min(100%,300px),1fr))]">
      <div>
        <div className="flex flex-wrap gap-x-7 gap-y-2.5 pb-1.5 pt-3.5">
          {data.outstanding.map((money) => {
            const overdue = overdueFor(data.overdue, money.currencyCode);
            return (
              <div key={money.currencyCode} data-testid={`outstanding-${money.currencyCode}`}>
                <div className="text-[11px] font-bold uppercase tracking-wider text-ink-muted">
                  Outstanding · {money.currencyCode}
                </div>
                <div className="mt-0.5 text-[22px] font-bold text-ink">{formatMoney(money)}</div>
                {overdue ? (
                  <p className="mt-0.5 flex items-center gap-1 text-[13px] font-semibold text-danger-ink">
                    <TriangleAlert size={13} aria-hidden="true" />
                    {formatMoney(overdue)} overdue
                  </p>
                ) : (
                  <p className="mt-0.5 text-[13px] text-ink-muted">None overdue</p>
                )}
              </div>
            );
          })}
        </div>
        {data.outstanding.length > 1 && (
          <p className="mb-3 text-[13px] text-ink-muted">
            Shown per currency — amounts in different currencies are never added together.
          </p>
        )}
        <div className="rounded-[var(--radius-sm)] bg-surface-sunken px-3.5 py-3 text-[13px] leading-relaxed">
          <p>
            <b>How to pay.</b> {data.howToPay}
          </p>
          <ClinicContacts />
        </div>
      </div>
      <div>
        <p className="mb-0.5 mt-3.5 text-[11px] font-bold uppercase tracking-wider text-ink-muted">
          {openInvoicesLabel(data.openInvoiceCount, data.invoices.length)}
        </p>
        <ul>
          {data.invoices.map((invoice) => (
            <TileRow
              key={invoice.invoiceId}
              main={
                <>
                  <Link
                    href={portalInvoiceHref(invoice.invoiceId)}
                    className="text-[13.5px] font-bold text-ink no-underline hover:underline"
                  >
                    {invoice.invoiceNumber ?? 'Invoice'}
                  </Link>
                  <p className="mt-0.5 text-[13px] text-ink-muted">
                    {invoice.issuedOn ? `Issued ${formatDateOnly(invoice.issuedOn)}` : 'Issued'}
                    {invoice.dueOn ? ` · due ${formatDateOnly(invoice.dueOn)}` : ''}
                  </p>
                </>
              }
              side={
                <>
                  <b>{formatMoney(invoice.balanceDue)}</b>
                  <StatusBadge status={invoiceStatusLabel(invoice.status)} />
                </>
              }
            />
          ))}
        </ul>
      </div>
    </div>
  );
}
