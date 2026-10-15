'use client';

import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { SurgeryStatusBadge } from '@/components/surgery/SurgeryStatusBadge';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { formatMoney } from '@/lib/format/money';
import type { CampaignCase, CampaignCases } from '@/lib/api/surgery-campaigns';

/**
 * `CampaignCasesTable` — HO-SRG-002 §11.7, US-SRG-11 "reportable separately". The campaign's cases with the
 * campaign price and the standard price, and a totals row from the SERVER's `totals` (which cover every
 * case, not just this page): *"12 cases · ৳24,000 campaign · ৳60,000 standard"*. Export is HO-RPT-002's.
 */
export function CampaignCasesTable({
  data,
  zone,
  isLoading,
  isError,
  onRetry,
  onPageChange,
}: {
  data: CampaignCases | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onPageChange: (page: number) => void;
}) {
  const columns: Array<Column<CampaignCase>> = [
    { id: 'cat', header: 'Cat', primary: true, cell: (c) => <strong>{c.catName}</strong> },
    { id: 'procedure', header: 'Procedure', cell: (c) => c.procedure },
    { id: 'date', header: 'Date', cell: (c) => formatAppointmentWhen(c.startsOn, zone) },
    { id: 'status', header: 'Status', cell: (c) => <SurgeryStatusBadge status={c.status} /> },
    {
      id: 'campaign',
      header: 'Campaign price',
      numeric: true,
      cell: (c) => formatMoney(c.campaignPrice),
    },
    {
      id: 'standard',
      header: 'Standard price',
      numeric: true,
      cell: (c) => (c.standardPrice ? formatMoney(c.standardPrice) : '—'),
    },
    { id: 'note', header: 'Eligibility', cell: (c) => c.eligibilityNote ?? '—' },
  ];
  const totals = data?.totals;
  return (
    <ResponsiveTable
      caption="Campaign cases"
      columns={columns}
      rows={data?.cases}
      getRowId={(c) => c.surgeryId}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{ title: 'No cases in this period.' }}
      pager={
        data
          ? {
              pageNumber: data.pageNumber,
              pageSize: data.pageSize,
              totalCount: data.totalCases,
              onPageChange,
            }
          : undefined
      }
      footer={
        totals && (
          <p className="text-sm font-semibold text-ink" aria-label="Totals">
            {totals.count} {totals.count === 1 ? 'case' : 'cases'} ·{' '}
            {formatMoney(totals.campaignTotal)} campaign · {formatMoney(totals.standardTotal)}{' '}
            standard
          </p>
        )
      }
    />
  );
}
