'use client';

import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { AmountCell } from '@/components/billing/Amount';
import { SelectField, TextField } from '@/components/billing/Fields';
import { PageHeader } from '@/components/billing/PageHeader';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { ManifestDownload } from '@/components/fulfilment/ManifestDownload';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { StaffOrderRow } from '@/lib/api/staff-orders';
import {
  ageSortKey,
  dayEndInstant,
  dayStartInstant,
  fulfilmentLabel,
  formatAgeMinutes,
  formatOrderWhen,
  orderStatusLabel,
  paymentMethodLabel,
} from '@/lib/format/orders';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useStaffOrderQueue } from '@/lib/hooks/useStaffOrders';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { OrderFlagBadge, OrderStatusBadge } from './OrderStatusBadge';
import { StaffOrdersTabs } from './StaffOrdersTabs';

/**
 * `components/orders/StaffOrderQueue.tsx` — ⭐ HO-ORD-002 §11.8, US-ORD-09, UI-4/UI-5. Filters (status,
 * fulfilment, payment method, channel, date range, search) live in the URL; the loaded page sorts by
 * placed-on or age; paging is the server's. Row flags are text chips. The list refetches every 30 s
 * (the filters are URL state, so a refetch never touches what the person typed) and becomes cards
 * below 768 px.
 */
const FIELDS = ['status', 'fulfilment', 'paymentMethod', 'channel', 'from', 'to', 'search', 'page'] as const;
const PAGE_SIZE = 25;
const STATUSES = ['PendingPayment', 'Confirmed', 'Packing', 'Dispatched', 'Delivered', 'Cancelled', 'Returned', 'Refunded'];
const FULFILMENTS = ['Delivery', 'Pickup', 'Counter'];
const PAYMENTS = ['CashOnDelivery', 'BankTransfer', 'PayOnCollection', 'InStore'];

export function StaffOrderQueue() {
  const { values, setValue, flush } = useUrlFilterState(FIELDS, {}, ['search']);
  const zone = useBranchTimeZone();
  const filters = {
    status: values.status || undefined,
    fulfilment: values.fulfilment || undefined,
    paymentMethod: values.paymentMethod || undefined,
    channel: values.channel || undefined,
    // The date inputs give a calendar day; the API takes instants - read the day in the branch's zone.
    from: values.from ? dayStartInstant(values.from, zone) : undefined,
    to: values.to ? dayEndInstant(values.to, zone) : undefined,
    search: values.search.trim() || undefined,
  };
  const query = useStaffOrderQueue({ ...filters, pageNumber: Number(values.page) || 1, pageSize: PAGE_SIZE });

  function set(name: (typeof FIELDS)[number], value: string) {
    setValue(name, value);
    if (name !== 'page') setValue('page', '');
  }

  const columns: Array<Column<StaffOrderRow>> = [
    { id: 'number', header: 'Order', primary: true, sortValue: (o) => o.orderNumber, cell: (o) => o.orderNumber },
    { id: 'placed', header: 'Placed', nowrap: true, sortValue: (o) => o.placedOn, cell: (o) => formatOrderWhen(o.placedOn, o.branchTimeZoneId) },
    { id: 'age', header: 'Age', nowrap: true, sortValue: (o) => ageSortKey(o.ageMinutes), cell: (o) => formatAgeMinutes(o.ageMinutes) },
    { id: 'customer', header: 'Customer', sortValue: (o) => o.customerName, cell: (o) => o.customerName },
    { id: 'status', header: 'Status', nowrap: true, cell: (o) => <OrderStatusBadge status={o.status} /> },
    { id: 'fulfilment', header: 'Fulfilment', cell: (o) => fulfilmentLabel(o.fulfilmentMethod) },
    { id: 'payment', header: 'Payment', cell: (o) => paymentMethodLabel(o.paymentMethod) },
    { id: 'total', header: 'Total', numeric: true, cell: (o) => <AmountCell money={o.grandTotal} /> },
    {
      id: 'flags',
      header: 'Flags',
      cell: (o) =>
        o.flags.length === 0 ? (
          <span className="sr-only">None</span>
        ) : (
          <span className="flex flex-wrap gap-1">
            {o.flags.map((flag) => (
              <OrderFlagBadge key={flag} flag={flag} />
            ))}
          </span>
        ),
    },
  ];

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  const filtered = FIELDS.some((name) => name !== 'page' && values[name]);

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Orders" description="Every order at the active branch, newest first. The list refreshes every 30 seconds." />
      <StaffOrdersTabs />
      <ManifestDownload />
      <form role="search" aria-label="Order filters" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4" onSubmit={(event) => event.preventDefault()}>
        <TextField
          id="oq-search"
          label="Search"
          type="search"
          placeholder="Number, name or phone"
          value={values.search}
          onChange={(event) => set('search', event.target.value)}
          onBlur={() => flush('search')}
        />
        <SelectField id="oq-status" label="Status" value={values.status} onChange={(e) => set('status', e.target.value)}>
          <option value="">Any status</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{orderStatusLabel(s)}</option>
          ))}
        </SelectField>
        <SelectField id="oq-fulfilment" label="Fulfilment" value={values.fulfilment} onChange={(e) => set('fulfilment', e.target.value)}>
          <option value="">Any</option>
          {FULFILMENTS.map((f) => (
            <option key={f} value={f}>{fulfilmentLabel(f)}</option>
          ))}
        </SelectField>
        <SelectField id="oq-payment" label="Payment method" value={values.paymentMethod} onChange={(e) => set('paymentMethod', e.target.value)}>
          <option value="">Any</option>
          {PAYMENTS.map((p) => (
            <option key={p} value={p}>{paymentMethodLabel(p)}</option>
          ))}
        </SelectField>
        <SelectField id="oq-channel" label="Channel" value={values.channel} onChange={(e) => set('channel', e.target.value)}>
          <option value="">Online and counter</option>
          <option value="Online">Online</option>
          <option value="Counter">Counter</option>
        </SelectField>
        <TextField id="oq-from" label="Placed from" type="date" value={values.from} onChange={(e) => set('from', e.target.value)} />
        <TextField id="oq-to" label="Placed to" type="date" value={values.to} onChange={(e) => set('to', e.target.value)} />
      </form>
      {filtered && (
        <div>
          <Button variant="ghost" size="sm" onClick={() => FIELDS.forEach((name) => setValue(name, ''))}>
            Clear filters
          </Button>
        </div>
      )}
      <ResponsiveTable
        caption="Orders"
        columns={columns}
        rows={query.data?.items}
        getRowId={(o) => o.id}
        rowHref={(o) => `/staff/orders/${o.id}`}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: 'No orders match.', description: filtered ? 'Change or clear the filters.' : 'New orders appear here as they are placed.' }}
        pager={
          query.data
            ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: (n) => setValue('page', String(n)) }
            : undefined
        }
      />
    </div>
  );
}
