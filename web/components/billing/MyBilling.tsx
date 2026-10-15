'use client';

import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { useMyBillingSummary, useMyInvoices, useMyLoyalty, useMyPayments } from '@/lib/hooks/billing/useMyBilling';
import type { InvoiceSummary, PaymentSummary } from '@/lib/api/billing-staff';
import { formatDateOnly } from '@/lib/format/time';
import { moneySign } from '@/lib/money';
import { Amount } from './Amount';
import { methodLabel } from './labels';
import { PageHeader, Section, Stat } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { ResponsiveTable, type Column } from './ResponsiveTable';
import { InvoiceStatusPill, PaymentStatusPill } from './StatusPill';

/**
 * `components/billing/MyBilling.tsx` — HO-BIL-002 §11.11 (US-BIL-03, 08, 10, customer). The customer's
 * billing page, on the warm customer layer: what they owe and have credit for, their issued invoices,
 * their payments with receipts, and loyalty points when enabled. Drafts and pending payments are
 * never shown (the server does not return them). There is **no pay-online control** anywhere — paying
 * is by bank transfer or at the clinic (see each invoice).
 */
export function MyBillingSummary() {
  const summary = useMyBillingSummary();
  return (
    <QueryBoundary query={summary} loadingLabel="Loading your balance…">
      {(s) => (
        <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="You owe" note={moneySign(s.outstanding.amount) === 'zero' ? 'Nothing outstanding' : undefined}>
            <Amount money={s.outstanding} />
          </Stat>
          <Stat label="Overdue" note={moneySign(s.overdue.amount) === 'positive' && s.oldestDueOn ? `Overdue since ${formatDateOnly(s.oldestDueOn)}` : undefined}>
            <Amount money={s.overdue} />
          </Stat>
          <Stat label="Your credit" note="Money the clinic holds for you; ask us to use it on an invoice.">
            <Amount money={s.freeCredit} />
          </Stat>
          {s.loyaltyEnabled && (
            <Stat label="Loyalty points">
              <span className="tabular-nums">{s.loyaltyPoints ?? 0}</span>
            </Stat>
          )}
        </dl>
      )}
    </QueryBoundary>
  );
}

export function MyInvoicesList() {
  const { values, setValue } = useUrlFilterState(['ipage'] as const);
  const invoices = useMyInvoices(values.ipage || 1);
  const columns: Array<Column<InvoiceSummary>> = [
    { id: 'number', header: 'Invoice', primary: true, cell: (i) => i.number ?? 'Invoice' },
    { id: 'date', header: 'Date', cell: (i) => (i.issueDate ? formatDateOnly(i.issueDate) : '—') },
    { id: 'status', header: 'Status', cell: (i) => <InvoiceStatusPill status={i.status} /> },
    { id: 'total', header: 'Total', numeric: true, cell: (i) => <Amount money={i.total} /> },
    { id: 'outstanding', header: 'Outstanding', numeric: true, cell: (i) => <Amount money={i.outstanding} /> },
  ];
  return (
    <ResponsiveTable
      caption="Your invoices"
      columns={columns}
      rows={invoices.data?.items}
      getRowId={(i) => i.invoiceId}
      rowHref={(i) => `/portal/billing/invoices/${i.invoiceId}`}
      isLoading={invoices.isLoading}
      isError={invoices.isError}
      onRetry={() => void invoices.refetch()}
      emptyState={{ title: 'You have no invoices yet.', description: 'When the clinic or the shop bills you, the invoice appears here.' }}
      pager={
        invoices.data
          ? { pageNumber: invoices.data.pageNumber, pageSize: invoices.data.pageSize, totalCount: invoices.data.totalCount, onPageChange: (n) => setValue('ipage', String(n)) }
          : undefined
      }
    />
  );
}

export function MyPaymentsList() {
  const { values, setValue } = useUrlFilterState(['ppage'] as const);
  const payments = useMyPayments(values.ppage || 1);
  const columns: Array<Column<PaymentSummary>> = [
    {
      id: 'receipt',
      header: 'Receipt',
      primary: true,
      cell: (p) => <span className={p.status === 'Reversed' ? 'line-through' : undefined}>{p.receiptNumber ?? 'Receipt'}</span>,
    },
    { id: 'date', header: 'Received', cell: (p) => formatDateOnly(p.receivedOn) },
    { id: 'method', header: 'Method', cell: (p) => methodLabel(p.method) },
    { id: 'status', header: 'Status', cell: (p) => <PaymentStatusPill status={p.status} /> },
    { id: 'amount', header: 'Amount', numeric: true, cell: (p) => <Amount money={p.amount} /> },
  ];
  return (
    <ResponsiveTable
      caption="Your payments"
      columns={columns}
      rows={payments.data?.items}
      getRowId={(p) => p.paymentId}
      rowHref={(p) => `/portal/billing/payments/${p.paymentId}`}
      isLoading={payments.isLoading}
      isError={payments.isError}
      onRetry={() => void payments.refetch()}
      emptyState={{ title: 'No payments yet.' }}
      pager={
        payments.data
          ? { pageNumber: payments.data.pageNumber, pageSize: payments.data.pageSize, totalCount: payments.data.totalCount, onPageChange: (n) => setValue('ppage', String(n)) }
          : undefined
      }
    />
  );
}

export function MyLoyaltySection() {
  const summary = useMyBillingSummary();
  const enabled = summary.data?.loyaltyEnabled ?? false;
  const loyalty = useMyLoyalty(enabled);
  if (!enabled) return null;
  return (
    <Section title="Loyalty points" description="Points you have earned and used.">
      <QueryBoundary query={loyalty} loadingLabel="Loading your points…">
        {(data) => (
          <div className="flex flex-col gap-2 text-sm">
            <p className="font-semibold text-ink">Balance: {data.balance} points</p>
            {data.entries.items.length === 0 ? (
              <p className="text-ink-muted">No points activity yet.</p>
            ) : (
              <ul className="flex flex-col gap-1">
                {data.entries.items.map((e) => (
                  <li key={e.entryId}>
                    {e.kind.replace(/([a-z])([A-Z])/g, '$1 $2')}: {e.points} points
                    {e.invoiceNumber ? ` (${e.invoiceNumber})` : ''} — {formatDateOnly(e.occurredOn.slice(0, 10))}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </QueryBoundary>
    </Section>
  );
}

export function MyBillingPage() {
  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="My billing" description="What you owe, your invoices and your payments." />
      <MyBillingSummary />
      <Section title="Invoices">
        <MyInvoicesList />
      </Section>
      <Section title="Payments and receipts">
        <MyPaymentsList />
      </Section>
      <MyLoyaltySection />
      <p className="text-sm text-ink-muted">
        You can pay by bank transfer (the details are on each invoice) or at the clinic by cash or card.
      </p>
    </div>
  );
}
