import { CheckCircle2, XCircle } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { Badge } from '@/components/ui/badge';
import type { StaffAccountSummary } from '@/lib/api/staff-admin';

/** `components/admin/StaffAccountTable.tsx` — US-ACC-07, UI-4 (paginated, filterable). */
export interface StaffAccountTableProps {
  rows: StaffAccountSummary[] | undefined;
  totalCount: number | undefined;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  roleIds: string[];
  roleFilter: string;
  onRoleFilterChange: (roleId: string) => void;
}

export function StaffAccountTable({
  rows,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  roleIds,
  roleFilter,
  onRoleFilterChange,
}: StaffAccountTableProps) {
  const columns: Array<DataTableColumn<StaffAccountSummary>> = [
    { id: 'name', header: 'Name', cell: (row) => row.fullName, sortable: true },
    {
      id: 'identifier',
      header: 'Identifier',
      cell: (row) => row.mobileNumber ?? row.emailAddress ?? '—',
    },
    { id: 'roles', header: 'Roles', cell: (row) => row.roleIds.join(', ') },
    {
      id: 'hasPassword',
      header: 'Password set',
      cell: (row) =>
        row.hasPassword ? (
          <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
            Yes
          </Badge>
        ) : (
          <Badge variant="warning" icon={<XCircle size={12} aria-hidden="true" />}>
            No
          </Badge>
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <label htmlFor="role-filter" className="text-sm font-semibold text-ink">
          Filter by role
        </label>
        <select
          id="role-filter"
          value={roleFilter}
          onChange={(event) => onRoleFilterChange(event.target.value)}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <option value="">All roles</option>
          {roleIds.map((roleId) => (
            <option key={roleId} value={roleId}>
              {roleId}
            </option>
          ))}
        </select>
      </div>

      {/* HO-ACC-002 §1 Amended (2026-09-26) — the horizontal-overflow-at-360px workaround that
          used to live here (an `overflow-x-auto` wrapper around this call site only) was fixed at
          the source instead: `components/common/DataTable` now wraps its own `<table>` in a
          keyboard-focusable scrollable region (integrator-authorised edit). */}
      <DataTable
        caption="Staff accounts"
        columns={columns}
        rows={rows}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        rowHref={(row) => `/staff/admin/staff-accounts/${row.id}`}
        search={{ label: 'Search staff accounts', placeholder: 'Name or identifier' }}
        pagination={{ totalCount: totalCount ?? 0, pageSize }}
        emptyState={{ title: 'No staff accounts match this search' }}
      />
    </div>
  );
}
