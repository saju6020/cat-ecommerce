'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { BUSINESS_LINES, invoicesCsvPath, type InvoiceSummary, type InvoicesParams } from '@/lib/api/billing-staff';
import { formatDateOnly } from '@/lib/format/time';
import { useInvoices } from '@/lib/hooks/billing/useBillingQueries';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { CARD_NUMBER_MESSAGE, looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';
import { Amount } from './Amount';
import { DownloadButton } from './DownloadButton';
import { TextField, SelectField } from './Fields';
import { PageHeader } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';
import { InvoiceStatusPill } from './StatusPill';

/**
 * `components/billing/InvoiceListScreen.tsx` — HO-BIL-002 §3.2 (US-BIL-01, 02, 05), UI-4. Filter by
 * status, number, issue-date range and business line (all in the URL), sort the loaded page, page
 * through the server's pages and **export CSV** (the server's injection-guarded CSV). Status is text
 * plus a glyph; money columns are right-aligned.
 */
const FIELDS = ['status', 'number', 'from', 'to', 'businessLine', 'page'] as const;
const PAGE_SIZE = 25;
const STATUSES = ['Draft', 'Issued', 'PartPaid', 'Paid', 'Credited', 'Discarded'];
const STATUS_LABELS: Record<string, string> = { PartPaid: 'Part-paid' };

export function InvoiceListScreen() {
  const { values, setValue } = useUrlFilterState(FIELDS, {}, ['number']);
  const [numberError, setNumberError] = useState<string | undefined>();
  const filters: InvoicesParams = {
    status: values.status || undefined,
    number: values.number || undefined,
    from: values.from || undefined,
    to: values.to || undefined,
    businessLine: values.businessLine || undefined,
  };
  const query = useInvoices({ ...filters, pageNumber: values.page || undefined, pageSize: PAGE_SIZE });

  function set(name: (typeof FIELDS)[number], value: string) {
    setValue(name, value);
    if (name !== 'page') setValue('page', '');
  }

  const columns: Array<Column<InvoiceSummary>> = [
    { id: 'number', header: 'Invoice', primary: true, sortValue: (i) => i.number ?? '~', cell: (i) => i.number ?? 'Draft' },
    { id: 'customer', header: 'Customer', sortValue: (i) => i.customerName, cell: (i) => i.customerName },
    { id: 'status', header: 'Status', cell: (i) => <InvoiceStatusPill status={i.status} /> },
    { id: 'issued', header: 'Issued', sortValue: (i) => i.issueDate ?? '', cell: (i) => (i.issueDate ? formatDateOnly(i.issueDate) : '—') },
    { id: 'due', header: 'Due', sortValue: (i) => i.dueOn ?? '', cell: (i) => (i.dueOn ? formatDateOnly(i.dueOn) : '—') },
    { id: 'total', header: 'Total', numeric: true, cell: (i) => <Amount money={i.total} /> },
    { id: 'paid', header: 'Paid', numeric: true, cell: (i) => <Amount money={i.paid} /> },
    { id: 'outstanding', header: 'Outstanding', numeric: true, cell: (i) => <Amount money={i.outstanding} /> },
  ];

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Invoices"
        description="Every invoice at the active branch, newest first."
        actions={
          <DownloadButton path={invoicesCsvPath(filters)} fallbackName="invoices.csv" label="Export CSV" />
        }
      />
      <form
        role="search"
        aria-label="Invoice filters"
        className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5"
        onSubmit={(event) => event.preventDefault()}
      >
        <SelectField id="inv-status" label="Status" value={values.status} onChange={(e) => set('status', e.target.value)}>
          <option value="">Any status</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {STATUS_LABELS[s] ?? s}
            </option>
          ))}
        </SelectField>
        <TextField
          id="inv-number"
          label="Invoice number"
          value={values.number}
          error={numberError}
          autoComplete="off"
          onChange={(e) => {
            // C-5: a card number is never written to the URL, the history or a request log.
            if (looksLikeCardNumber(e.target.value)) {
              setNumberError(CARD_NUMBER_MESSAGE);
              return;
            }
            setNumberError(undefined);
            set('number', e.target.value);
          }}
        />
        <TextField id="inv-from" label="Issued from" type="date" value={values.from} onChange={(e) => set('from', e.target.value)} />
        <TextField id="inv-to" label="Issued to" type="date" value={values.to} onChange={(e) => set('to', e.target.value)} />
        <SelectField id="inv-line" label="Business line" value={values.businessLine} onChange={(e) => set('businessLine', e.target.value)}>
          <option value="">All lines</option>
          {BUSINESS_LINES.map((l) => (
            <option key={l} value={l}>
              {l}
            </option>
          ))}
        </SelectField>
      </form>
      {(values.status || values.number || values.from || values.to || values.businessLine) && (
        <div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => FIELDS.forEach((name) => setValue(name, ''))}
          >
            Clear filters
          </Button>
        </div>
      )}
      <ResponsiveTable
        caption="Invoices"
        columns={columns}
        rows={query.data?.items}
        getRowId={(i) => i.invoiceId}
        rowHref={(i) => `/staff/billing/invoices/${i.invoiceId}`}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: 'No invoices match.', description: 'Change or clear the filters, or start from a customer’s billing account.' }}
        pager={
          query.data
            ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: (n) => setValue('page', String(n)) }
            : undefined
        }
      />
    </div>
  );
}
