'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Download } from 'lucide-react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { Button } from '@/components/ui/button';
import type { AgeingRow } from '@/lib/api/billing-staff';
import { ApiForbiddenError } from '@/lib/api/client';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useAgeing } from '@/lib/hooks/billing/useBillingQueries';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { Amount } from './Amount';
import { buildAgeingCsv } from './ageingCsv';
import { TextField } from './Fields';
import { PageHeader, Section, Stat } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/AgeingTable.tsx` — HO-BIL-002 §11.5 (US-BIL-03 AC2). Receivables ageing as of a
 * date (default today in the branch's zone): the bucket totals and one row per customer, every amount
 * the server's string (*Current, 1–30, 31–60, 61–90, 90+ days*), a link to each account, sortable by
 * oldest due, and **Export CSV** built from the same rows as text.
 */
const FIELDS = ['asOf', 'page'] as const;
const PAGE_SIZE = 50;

export function AgeingTable() {
  const { zone, ready } = useActiveBranchZone();
  const { values, setValue } = useUrlFilterState(FIELDS);
  const query = useAgeing({ asOf: values.asOf || undefined, pageNumber: values.page || undefined, pageSize: PAGE_SIZE });
  const [csvUrl, setCsvUrl] = useState<string | null>(null);

  function exportCsv(rows: AgeingRow[]) {
    if (csvUrl) URL.revokeObjectURL(csvUrl);
    const url = URL.createObjectURL(new Blob([buildAgeingCsv(rows)], { type: 'text/csv;charset=utf-8' }));
    setCsvUrl(url);
    const link = document.createElement('a');
    link.href = url;
    link.download = `receivables-ageing-${query.data?.asOf ?? 'today'}.csv`;
    link.click();
  }

  const columns: Array<Column<AgeingRow>> = [
    {
      id: 'customer',
      header: 'Customer',
      primary: true,
      sortValue: (r) => r.customerName,
      cell: (r) => (
        <Link href={`/staff/billing/customers/${r.customerId}`} className="font-semibold text-accent-ink hover:underline">
          {r.customerName}
        </Link>
      ),
    },
    { id: 'current', header: 'Current', numeric: true, cell: (r) => <Amount money={r.current} /> },
    { id: 'd30', header: '1–30 days', numeric: true, cell: (r) => <Amount money={r.days1To30} /> },
    { id: 'd60', header: '31–60 days', numeric: true, cell: (r) => <Amount money={r.days31To60} /> },
    { id: 'd90', header: '61–90 days', numeric: true, cell: (r) => <Amount money={r.days61To90} /> },
    { id: 'over', header: '90+ days', numeric: true, cell: (r) => <Amount money={r.over90} /> },
    { id: 'total', header: 'Total', numeric: true, cell: (r) => <Amount money={r.total} /> },
    { id: 'oldest', header: 'Oldest due', sortValue: (r) => r.oldestDueOn, cell: (r) => formatDateOnly(r.oldestDueOn) },
  ];

  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  const data = query.data;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Receivables ageing"
        description="What customers owe, by how long it has been overdue."
        actions={
          <Button variant="secondary" size="sm" disabled={!data || data.rows.items.length === 0} onClick={() => data && exportCsv(data.rows.items)}>
            <Download size={14} aria-hidden="true" />
            Export CSV
          </Button>
        }
      />
      <form role="search" aria-label="Ageing date" className="max-w-xs" onSubmit={(e) => e.preventDefault()}>
        <TextField id="ageing-as-of" label="As of" type="date" value={values.asOf || query.data?.asOf || ''} max={ready ? todayInZone(zone) : undefined} onChange={(e) => { setValue('asOf', e.target.value); setValue('page', ''); }} />
      </form>
      {data && (
        <Section title={`Totals as of ${formatDateOnly(data.asOf)}`}>
          <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <Stat label="Current"><Amount money={data.totals.current} /></Stat>
            <Stat label="1–30 days"><Amount money={data.totals.days1To30} /></Stat>
            <Stat label="31–60 days"><Amount money={data.totals.days31To60} /></Stat>
            <Stat label="61–90 days"><Amount money={data.totals.days61To90} /></Stat>
            <Stat label="90+ days"><Amount money={data.totals.over90} /></Stat>
          </dl>
        </Section>
      )}
      <ResponsiveTable
        caption="Receivables by customer"
        columns={columns}
        rows={data?.rows.items}
        getRowId={(r) => r.customerId}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: 'Nobody owes anything as of this date.' }}
        pager={
          data
            ? { pageNumber: data.rows.pageNumber, pageSize: data.rows.pageSize, totalCount: data.rows.totalCount, onPageChange: (n) => setValue('page', String(n)) }
            : undefined
        }
      />
    </div>
  );
}
