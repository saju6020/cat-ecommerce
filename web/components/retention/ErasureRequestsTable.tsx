'use client';

import { CircleCheck, Clock } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { Badge } from '@/components/ui/badge';
import type { ErasureRequest, ErasureRequestStatus } from '@/lib/api/retention';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { SOURCE_LABEL } from '@/lib/retention/labels';

/** "2 of 3 complete, 1 held until 12 Mar 2032" */
export function summariseScopes(request: ErasureRequest): string {
  const total = request.scopes.length;
  const complete = request.scopes.filter((s) => s.status === 'Completed').length;
  const held = request.scopes.filter((s) => s.status === 'Held');
  const failing = request.scopes.filter((s) => s.status === 'Failing').length;
  const parts = [`${complete} of ${total} complete`];
  const heldUntil = held
    .map((s) => s.heldUntil)
    .filter((d): d is string => d !== null)
    .sort()
    .at(-1);
  if (held.length > 0) {
    parts.push(`${held.length} held${heldUntil ? ` until ${formatDateOnly(heldUntil)}` : ''}`);
  }
  if (failing > 0) parts.push(`${failing} retrying`);
  return parts.join(', ');
}

/**
 * `components/retention/ErasureRequestsTable.tsx` — HO-XCT-002 §11.5, U-B4. The erasure ledger. It
 * identifies a customer by account id only: the name, phone and email no longer exist, and this
 * table does not look for them. The status filter is a real server filter (`status=Open|Completed`).
 */
export function ErasureRequestsTable({
  requests,
  totalCount,
  pageSize,
  status,
  onStatusChange,
  isLoading,
  isError,
  onRetry,
  zone,
}: {
  requests: ErasureRequest[] | undefined;
  totalCount: number | undefined;
  pageSize: number;
  status: ErasureRequestStatus | '';
  onStatusChange: (status: ErasureRequestStatus | '') => void;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  zone: string;
}) {
  const columns: Array<DataTableColumn<ErasureRequest>> = [
    { id: 'customer', header: 'Customer account', cell: (r) => <span className="font-mono text-xs">{r.customerAccountId}</span> },
    { id: 'requested', header: 'Requested', cell: (r) => formatInZone(r.requestedOn, zone) },
    { id: 'source', header: 'Source', cell: (r) => SOURCE_LABEL[r.source] ?? 'Other' },
    {
      id: 'status',
      header: 'Status',
      cell: (r) =>
        r.status === 'Completed' ? (
          <Badge variant="success" icon={<CircleCheck size={12} aria-hidden="true" />}>Completed</Badge>
        ) : (
          <Badge variant="warning" icon={<Clock size={12} aria-hidden="true" />}>Open</Badge>
        ),
    },
    { id: 'scopes', header: 'Parts', cell: summariseScopes },
  ];

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <label htmlFor="erasure-status-filter" className="font-semibold text-ink">
          Status
        </label>
        <select
          id="erasure-status-filter"
          value={status}
          onChange={(e) => onStatusChange(e.target.value as ErasureRequestStatus | '')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-ink"
        >
          <option value="">All</option>
          <option value="Open">Open</option>
          <option value="Completed">Completed</option>
        </select>
      </div>
      <DataTable
        caption="Erasure requests, newest first"
        columns={columns}
        rows={requests}
        getRowId={(r) => r.customerAccountId}
        rowHref={(r) => `/staff/admin/retention/erasures/${r.customerAccountId}`}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{
          title: 'No erasure requests',
          description: 'Requests appear here when a customer asks, or staff take one on their behalf.',
        }}
        pagination={totalCount !== undefined ? { totalCount, pageSize } : undefined}
      />
    </div>
  );
}
