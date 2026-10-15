'use client';

import Link from 'next/link';
import { useState } from 'react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { AmountCell } from '@/components/billing/Amount';
import { SelectField } from '@/components/billing/Fields';
import { PageHeader } from '@/components/billing/PageHeader';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { ReconciliationIssue } from '@/lib/api/staff-orders';
import { formatOrderWhen, reconciliationKindLabel } from '@/lib/format/orders';
import { useReconciliationIssues, useResolveReconciliationIssue } from '@/lib/hooks/useStaffOrders';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { OrderReasonDialog } from './OrderReasonDialog';
import { StaffOrdersTabs } from './StaffOrdersTabs';

/**
 * `components/orders/ReconciliationIssuesList.tsx` — ⭐ HO-ORD-002 §11.9, US-ORD-08 AC2. When a payment was
 * taken but the order could not be completed, an issue is raised so nobody loses track of the money.
 * Each row shows what was entered (method, amount, reference, when received) and what failed; **Resolve**
 * needs a note. The list is for the active branch only (the server answers `403`/`404` otherwise).
 */
const PAGE_SIZE = 20;

export function ReconciliationIssuesList() {
  const [status, setStatus] = useState('Open');
  const [page, setPage] = useState(1);
  const query = useReconciliationIssues({ status: status || undefined, pageNumber: page, pageSize: PAGE_SIZE });
  const resolve = useResolveReconciliationIssue();
  const zone = useBranchTimeZone();
  const [resolving, setResolving] = useState<ReconciliationIssue | null>(null);
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  const columns: Array<Column<ReconciliationIssue>> = [
    { id: 'raised', header: 'Raised', primary: true, sortValue: (i) => i.raisedOn, cell: (i) => formatOrderWhen(i.raisedOn, zone) },
    { id: 'kind', header: 'What failed', cell: (i) => reconciliationKindLabel(i.kind) },
    {
      id: 'order',
      header: 'Order',
      cell: (i) => (i.orderId ? <Link href={`/staff/orders/${i.orderId}`} className="text-accent-ink hover:underline">Open the order</Link> : 'No order'),
    },
    { id: 'method', header: 'Payment method', cell: (i) => i.paymentMethod },
    { id: 'amount', header: 'Amount entered', numeric: true, cell: (i) => <AmountCell money={i.amount} /> },
    { id: 'reference', header: 'Reference', cell: (i) => i.paymentReference ?? 'None' },
    { id: 'received', header: 'Received', cell: (i) => (i.paymentReceivedOn ? formatOrderWhen(i.paymentReceivedOn, zone) : 'Not recorded') },
    { id: 'summary', header: 'Problem', cell: (i) => i.errorSummary },
    {
      id: 'action',
      header: 'Status',
      cell: (i) =>
        i.status === 'Open' ? (
          <Button size="sm" onClick={() => setResolving(i)}>
            Resolve<span className="sr-only"> issue raised {formatOrderWhen(i.raisedOn, zone)}</span>
          </Button>
        ) : (
          <span>Resolved{i.resolutionNote ? `: ${i.resolutionNote}` : ''}</span>
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Reconciliation issues" description="Payments that were taken but could not be completed. Resolve each one with a note." />
      <StaffOrdersTabs />
      <div className="max-w-xs">
        <SelectField id="recon-status" label="Show" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
          <option value="Open">Open issues</option>
          <option value="Resolved">Resolved issues</option>
          <option value="">All issues</option>
        </SelectField>
      </div>
      <ResponsiveTable
        caption="Reconciliation issues"
        columns={columns}
        rows={query.data?.items}
        getRowId={(i) => i.id}
        isLoading={query.isLoading}
        isError={query.isError}
        onRetry={() => void query.refetch()}
        emptyState={{ title: status === 'Open' ? 'No open issues.' : 'No issues.', description: 'Every payment taken is matched to an order.' }}
        pager={query.data ? { pageNumber: query.data.pageNumber, pageSize: query.data.pageSize, totalCount: query.data.totalCount, onPageChange: setPage } : undefined}
      />
      <OrderReasonDialog
        open={!!resolving}
        onOpenChange={(next) => !next && setResolving(null)}
        title="Resolve this issue"
        consequence="Mark the issue resolved. Say what you checked or what you did about the payment."
        confirmLabel="Resolve issue"
        reasonLabel="Resolution note"
        variant="primary"
        onSubmit={(note) => (resolving ? resolve.mutateAsync({ issueId: resolving.id, note }) : Promise.resolve())}
      />
    </div>
  );
}
