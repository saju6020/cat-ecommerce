'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { ExportLink } from '@/components/inventory/ExportLink';
import { Banner } from '@/components/inventory/fields';
import { StackedTable, type StackedColumn } from '@/components/inventory/StackedTable';
import { traceKindLabel, referenceKindLabel } from '@/components/inventory/labels';
import { formatQuantity } from '@/lib/format/inventory';
import { formatInZone } from '@/lib/format/time';
import type { RecallTrace, RecallTraceCustomer } from '@/lib/api/inventory-recalls';
import { cn } from '@/lib/utils';

const PAGE_SIZE = 50;
type TabId = 'received' | 'returned' | 'unshipped' | 'unidentified';

/** Distinct, named customers across the trace — a headcount of people the trace names, not a stock
 *  figure. An anonymised customer has no contact details and cannot be messaged. */
export function countTraceCustomers(trace: RecallTrace): number {
  const ids = new Set<string>();
  const add = (customer: RecallTraceCustomer | null | undefined) => {
    if (customer && !customer.isAnonymised) ids.add(customer.customerId);
  };
  trace.received.forEach((r) => add(r.customer));
  trace.returned.forEach((r) => add(r.customer));
  trace.unshippedDispatchBlocked.forEach((r) => add(r.customer));
  return ids.size;
}

function Customer({ customer }: { customer: RecallTraceCustomer | null }) {
  if (!customer) return <span className="text-ink-muted">Not recorded</span>;
  return (
    <span className="flex flex-col">
      <span className="font-semibold">{customer.name}</span>
      {customer.originalCustomerId && <span className="text-xs text-ink-muted">Merged account — shown as the surviving customer</span>}
      {customer.isAnonymised ? (
        <span className="text-xs text-ink-muted">Anonymised — no contact details</span>
      ) : (
        <>
          {customer.mobileNumber && <span className="text-xs">{customer.mobileNumber}</span>}
          {customer.email && <span className="break-all text-xs">{customer.email}</span>}
        </>
      )}
    </span>
  );
}

/**
 * `components/recall/RecallTraceTable.tsx` — HO-IMP-002 §11.7, US-IMP-11 AC2. Four tabs over the
 * trace: **Received** (when, what kind of use in plain words — bought, dispensed, fed during
 * boarding — quantity, reference, the customer with phone and email, the cat and its current owner),
 * **Returned**, **Unshipped — dispatch blocked** and **Unidentified counter sales**. Paged 50 at a
 * time (OQ-5). Contact details are shown because this screen is `Recalls.Manage` only; they live in
 * the query cache for this view and nowhere else (never logged, never put in storage). **Export CSV**
 * is the server's (and the server audits it).
 */
export function RecallTraceTable({
  trace,
  stockUnit,
  csvUrl,
  timeZoneId,
}: {
  trace: RecallTrace;
  stockUnit: string;
  csvUrl: string;
  timeZoneId: string;
}) {
  const [tab, setTab] = React.useState<TabId>('received');
  const [page, setPage] = React.useState(1);
  const tabs: Array<{ id: TabId; label: string; count: number }> = [
    { id: 'received', label: 'Received', count: trace.received.length },
    { id: 'returned', label: 'Returned', count: trace.returned.length },
    { id: 'unshipped', label: 'Unshipped — dispatch blocked', count: trace.unshippedDispatchBlocked.length },
    { id: 'unidentified', label: 'Unidentified counter sales', count: trace.unidentified.count },
  ];
  const paged = <T,>(rows: T[]) => rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const total = tab === 'received' ? trace.received.length : tab === 'returned' ? trace.returned.length : tab === 'unshipped' ? trace.unshippedDispatchBlocked.length : 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const receivedColumns: Array<StackedColumn<RecallTrace['received'][number]>> = [
    { id: 'when', header: 'When', rowHeader: true, cell: (r) => formatInZone(r.occurredOn, timeZoneId) },
    { id: 'kind', header: 'How', cell: (r) => traceKindLabel(r.kind) },
    { id: 'qty', header: 'Quantity', numeric: true, cell: (r) => formatQuantity(r.quantity, stockUnit) },
    { id: 'ref', header: 'Reference', cell: (r) => r.referenceLabel ?? '—' },
    { id: 'customer', header: 'Customer', cell: (r) => <Customer customer={r.customer} /> },
    {
      id: 'cat',
      header: 'Cat and current owner',
      cell: (r) =>
        r.cat ? (
          <span>
            {r.cat.name}
            {r.cat.currentOwner && <span className="block text-xs text-ink-muted">Owner now: {r.cat.currentOwner.name}</span>}
          </span>
        ) : r.householdCats.length > 0 ? (
          <span>
            Household cats — not specifically recorded
            <span className="block text-xs text-ink-muted">{r.householdCats.join(', ')}</span>
          </span>
        ) : (
          '—'
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div role="tablist" aria-label="Trace sections" className="flex flex-wrap gap-2">
          {tabs.map((t) => (
            <button
              key={t.id}
              role="tab"
              type="button"
              id={`trace-tab-${t.id}`}
              aria-selected={tab === t.id}
              aria-controls="trace-panel"
              onClick={() => { setTab(t.id); setPage(1); }}
              className={cn(
                'rounded-full border px-3 py-1 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                tab === t.id ? 'border-accent bg-accent-soft text-accent-ink' : 'border-border text-ink-muted hover:bg-surface-sunken',
              )}
            >
              {t.label} ({t.count})
            </button>
          ))}
        </div>
        <ExportLink href={csvUrl} label="Export trace as CSV" />
      </div>
      <div id="trace-panel" role="tabpanel" aria-labelledby={`trace-tab-${tab}`} className="min-w-0">
        {tab === 'received' &&
          (trace.received.length === 0 ? (
            <p className="text-sm text-ink-muted">None of this lot has been used yet.</p>
          ) : (
            <StackedTable caption="Where this lot was used" rows={paged(trace.received)} getKey={(r, i) => `${r.occurredOn}-${i}`} columns={receivedColumns} />
          ))}
        {tab === 'returned' &&
          (trace.returned.length === 0 ? (
            <p className="text-sm text-ink-muted">Nothing from this lot has been returned.</p>
          ) : (
            <StackedTable
              caption="Returned stock"
              rows={paged(trace.returned)}
              getKey={(r) => r.referenceId}
              columns={[
                { id: 'ref', header: 'Return', rowHeader: true, cell: (r) => r.referenceLabel ?? referenceKindLabel(r.referenceKind) },
                { id: 'qty', header: 'Quantity', numeric: true, cell: (r) => formatQuantity(r.quantity, stockUnit) },
                { id: 'customer', header: 'Customer', cell: (r) => <Customer customer={r.customer} /> },
              ]}
            />
          ))}
        {tab === 'unshipped' &&
          (trace.unshippedDispatchBlocked.length === 0 ? (
            <p className="text-sm text-ink-muted">No order holds stock of this lot waiting to ship.</p>
          ) : (
            <div className="flex flex-col gap-2">
              <Banner tone="warning">These orders must not be dispatched while the lot is recalled.</Banner>
              <StackedTable
                caption="Unshipped orders holding this lot"
                rows={paged(trace.unshippedDispatchBlocked)}
                getKey={(r) => r.referenceId}
                columns={[
                  { id: 'ref', header: 'Order', rowHeader: true, cell: (r) => referenceKindLabel(r.referenceKind) },
                  { id: 'qty', header: 'Quantity held', numeric: true, cell: (r) => formatQuantity(r.quantity, stockUnit) },
                  { id: 'customer', header: 'Customer', cell: (r) => <Customer customer={r.customer} /> },
                  { id: 'cat', header: 'Cat', cell: (r) => r.cat?.name ?? '—' },
                ]}
              />
            </div>
          ))}
        {tab === 'unidentified' && (
          <p className="text-sm text-ink">
            {trace.unidentified.count === 0
              ? 'No counter sales of this lot are missing a customer.'
              : `${trace.unidentified.count} counter ${trace.unidentified.count === 1 ? 'sale' : 'sales'} (${formatQuantity(trace.unidentified.quantity, stockUnit)}) have no customer recorded, so those buyers cannot be contacted. Consider a notice at the till.`}
          </p>
        )}
      </div>
      {pages > 1 && (
        <nav aria-label="Trace pages" className="flex items-center justify-between text-sm text-ink-muted">
          <span>Page {page} of {pages}</span>
          <span className="flex gap-2">
            <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
            <Button size="sm" variant="secondary" disabled={page >= pages} onClick={() => setPage(page + 1)}>Next</Button>
          </span>
        </nav>
      )}
    </div>
  );
}
