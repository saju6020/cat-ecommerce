'use client';

import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { buttonVariants } from '@/components/ui/button';
import { DataTable } from '@/components/common/DataTable';
import type { BranchSummary } from '@/lib/api/admin-branches';

/** `components/branches/BranchTable.tsx` — HO-ADM-002 §11.2, US-ADM-01. Name, code, zone, active;
 *  create button shown only to a caller who can manage branches (presentation only). */
export interface BranchTableProps {
  branches: BranchSummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  canCreate: boolean;
}

export function BranchTable({
  branches,
  isLoading,
  isError,
  onRetry,
  canCreate,
}: BranchTableProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Branches</h1>
        {canCreate && (
          <Link href="/staff/admin/branches/new" className={buttonVariants({ variant: 'primary' })}>
            New branch
          </Link>
        )}
      </div>

      <DataTable
        caption="Branches"
        rows={branches}
        getRowId={(row) => row.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{
          title: 'No branches yet',
          description: canCreate
            ? 'Create the first branch to get started.'
            : 'None have been created yet.',
        }}
        rowHref={(row) => `/staff/admin/branches/${row.id}`}
        columns={[
          { id: 'name', header: 'Name', cell: (row) => row.name },
          { id: 'code', header: 'Code', cell: (row) => row.code },
          { id: 'timeZoneId', header: 'Time zone', cell: (row) => row.timeZoneId },
          {
            id: 'isActive',
            header: 'Status',
            cell: (row) => (
              <Badge variant={row.isActive ? 'success' : 'neutral'}>
                {row.isActive ? 'Active' : 'Inactive'}
              </Badge>
            ),
          },
        ]}
      />
    </div>
  );
}
