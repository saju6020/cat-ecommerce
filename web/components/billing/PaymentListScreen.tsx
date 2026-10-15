'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { Button } from '@/components/ui/button';
import { paymentsCsvPath, type PaymentSummary, type PaymentsParams } from '@/lib/api/billing-staff';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatDateOnly } from '@/lib/format/time';
import { usePaymentMethods, usePayments } from '@/lib/hooks/billing/useBillingQueries';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { Amount } from './Amount';
import { DownloadButton } from './DownloadButton';
import { SelectField, TextField } from './Fields';
import { methodLabel } from './labels';
import { PageHeader } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';
import { PaymentStatusPill } from './StatusPill';

/**
 * `components/billing/PaymentListScreen.tsx` — HO-BIL-002 §3.2 (US-BIL-11), UI-4. Filter by status,
 * method and date received (URL-backed), sort the loaded page, page through the server's pages and
 * export CSV. A reversed payment is struck through **and** says *Reversed* (never colour alone).
 */
const FIELDS = ['status', 'method', 'from', 'to', 'page'] as const;
const PAGE_SIZE = 25;
const STATUSES = ['PendingApproval', 'Accepted', 'Rejected', 'Reversed'];
const STATUS_LABELS: Record<string, string> = { PendingApproval: 'Pending approval' };

export function PaymentListScreen() {
  const { values, setValue } = useUrlFilterState(FIELDS);
  const filters: PaymentsParams = {
    status: values.status || undefined,
    method: values.method || undefined,
    from: values.from || undefined,
    to: values.to || undefined,
  };
  const methods = usePaymentMethods();
  const query = usePayments({ ...filters, pageNumber: values.page || undefined, pageSize: PAGE_SIZE });

  function set(name: (typeof FIELDS)[number], value: string) {
    setValue(name, value);
    if (name !== 'page') setValue('page', '');
  }

  const columns: Array<Column<PaymentSummary>> = [
    { id: 'receipt', header: 'Receipt', primary: true, sortValue: (p) => p.receiptNumber ?? '~', cell: (p) => p.receiptNumber ?? 'Awaiting approval' },
    { id: 'customer', header: 'Customer', sortValue: (p) => p.customerName, cell: (p) => p.customerName },
    { id: 'method', header: 'Method', cell: (p) => methodLabel(p.method) },
    { id: 'status', header: 'Status', cell: (p) => <PaymentStatusPill status={p.status} /> },
    { id: 'received', header: 'Received', sortValue: (p) => p.receivedOn, cell: (p) => formatDateOnly(p.receivedOn) },
    { id: 'amount', header: 'Amount', numeric: true, cell: (p) => <Amount money={p.amount} className={p.status === 'Reversed' ? 'line-through' : undefined} /> },
    { id: 'unallocated', header: 'Unallocated', numeric: true, cell: (p) => <Amount money={p.unallocated} /> },
    { id: 'by', header: 'Recorded by', cell: (p) => p.recordedByName },
  ];

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Payments"
        description="Every payment at the active branch, newest first."
        actions={
          <DownloadButton path={paymentsCsvPath(filters)} fallbackName="payments.csv" label="Export CSV" />
        }
      />
      <form role="search" aria-label="Payment filters" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4" onSubmit={(e) => e.preventDefault()}>
        <SelectField id="pay-f-status" label="Status" value={values.status} onChange={(e) => set('status', e.target.value)}>
          <option value="">Any status</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{STATUS_LABELS[s] ?? s}</option>
          ))}
        </SelectField>
        <SelectField id="pay-f-method" label="Method" value={values.method} onChange={(e) => set('method', e.target.value)}>
          <option value="">Any method</option>
          {(methods.data ?? []).map((m) => (
            <option key={m.name} value={m.name}>{methodLabel(m.name)}</option>
          ))}
        </SelectField>
        <TextField id="pay-f-from" label="Received from" type="date" value={values.from} onChange={(e) => set('from', e.target.value)} />
        <TextField id="pay-f-to" label="Received to" type="date" value={values.to} onChange={(e) => set('to', e.target.value)} />
      </form>
      {(values.status || values.method || values.from || values.to) && (
        <div>
          <Button variant="ghost" size="sm" onClick={() => FIELDS.forEach((name) => setValue(name, ''))}>
            Clear filters
          </Button>
        </div>
      )}
      <ResponsiveTable
        caption="Payments"
        columns={columns}
        rows={query.data?.items}
        getRowId={(p) => p.paymentId}
        rowHref={(p) => `/staff/billing/payments/${p.paymentId}`}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: 'No payments match.', description: 'Change or clear the filters.' }}
        pager={
          query.data
            ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: (n) => setValue('page', String(n)) }
            : undefined
        }
      />
    </div>
  );
}
