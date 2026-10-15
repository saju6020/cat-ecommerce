import Link from 'next/link';
import type { InvoiceSummary, PaymentSummary } from '@/lib/api/billing-staff';
import { formatDateOnly } from '@/lib/format/time';
import { Amount } from './Amount';
import { InvoiceStatusPill, PaymentStatusPill } from './StatusPill';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/CustomerAccountTables.tsx` — HO-BIL-002 §11.1. The account's **Invoices** and
 * **Payments** tabs: status as text, outstanding amounts, links to the documents.
 */
export function CustomerInvoicesTable({
  invoices,
  isLoading,
  isError,
  onRetry,
}: {
  invoices: InvoiceSummary[] | undefined;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}) {
  const columns: Array<Column<InvoiceSummary>> = [
    {
      id: 'number',
      header: 'Invoice',
      primary: true,
      cell: (i) => i.number ?? 'Draft',
    },
    { id: 'status', header: 'Status', cell: (i) => <InvoiceStatusPill status={i.status} /> },
    {
      id: 'issued',
      header: 'Issued',
      cell: (i) => (i.issueDate ? formatDateOnly(i.issueDate) : '—'),
    },
    { id: 'due', header: 'Due', cell: (i) => (i.dueOn ? formatDateOnly(i.dueOn) : '—') },
    { id: 'total', header: 'Total', numeric: true, cell: (i) => <Amount money={i.total} /> },
    {
      id: 'outstanding',
      header: 'Outstanding',
      numeric: true,
      cell: (i) => <Amount money={i.outstanding} />,
    },
  ];
  return (
    <ResponsiveTable
      caption="Invoices"
      columns={columns}
      rows={invoices}
      getRowId={(i) => i.invoiceId}
      rowHref={(i) => `/staff/billing/invoices/${i.invoiceId}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{ title: 'No invoices yet.', description: 'Select unbilled charges and choose New invoice.' }}
    />
  );
}

export function CustomerPaymentsTable({
  payments,
  isLoading,
  isError,
  onRetry,
}: {
  payments: PaymentSummary[] | undefined;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
}) {
  const columns: Array<Column<PaymentSummary>> = [
    {
      id: 'receipt',
      header: 'Receipt',
      primary: true,
      cell: (p) => p.receiptNumber ?? 'Awaiting approval',
    },
    { id: 'method', header: 'Method', cell: (p) => p.method },
    { id: 'status', header: 'Status', cell: (p) => <PaymentStatusPill status={p.status} /> },
    { id: 'received', header: 'Received', cell: (p) => formatDateOnly(p.receivedOn) },
    { id: 'amount', header: 'Amount', numeric: true, cell: (p) => <Amount money={p.amount} /> },
    {
      id: 'unallocated',
      header: 'Unallocated',
      numeric: true,
      cell: (p) => <Amount money={p.unallocated} />,
    },
  ];
  return (
    <ResponsiveTable
      caption="Payments"
      columns={columns}
      rows={payments}
      getRowId={(p) => p.paymentId}
      rowHref={(p) => `/staff/billing/payments/${p.paymentId}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{ title: 'No payments yet.' }}
    />
  );
}

export function AccountLink({ customerId, children }: { customerId: string; children: React.ReactNode }) {
  return (
    <Link href={`/staff/billing/customers/${customerId}`} className="font-semibold text-accent-ink hover:underline">
      {children}
    </Link>
  );
}
