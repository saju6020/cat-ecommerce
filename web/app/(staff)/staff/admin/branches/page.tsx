'use client';

import { useStaffSession } from '@/lib/hooks/useSession';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { BranchTable } from '@/components/branches/BranchTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/branches` — US-ADM-01. */
export default function BranchesPage() {
  const { data: session } = useStaffSession();
  const { data, isLoading, isError, error, refetch } = useAdminBranches();

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <BranchTable
      branches={data}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      canCreate={(session?.roleIds ?? []).includes('Administrator')}
    />
  );
}
