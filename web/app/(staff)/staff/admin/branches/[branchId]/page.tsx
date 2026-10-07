'use client';

import { use } from 'react';
import {
  useAdminBranch,
  useUpdateBranchDetails,
  useSetBranchActivation,
} from '@/lib/hooks/useAdminBranches';
import { BranchDetailsForm } from '@/components/branches/BranchDetailsForm';
import { LoadingState } from '@/components/common/LoadingState';

/** `/staff/admin/branches/[branchId]` — Details tab. */
export default function BranchDetailsPage({ params }: { params: Promise<{ branchId: string }> }) {
  const { branchId } = use(params);
  const { data: branch, isLoading } = useAdminBranch(branchId);
  const updateMutation = useUpdateBranchDetails(branchId);
  const activationMutation = useSetBranchActivation(branchId);

  if (isLoading || !branch) return <LoadingState rows={5} />;

  return (
    <BranchDetailsForm
      branch={branch}
      isPending={updateMutation.isPending}
      onSave={(values) =>
        updateMutation.mutateAsync({
          name: values.name,
          timeZoneId: values.timeZoneId,
          addressLine: values.addressLine,
          phoneNumber: values.phoneNumber,
          email: values.email,
        })
      }
      onSetActivation={(isActive) => activationMutation.mutateAsync(isActive)}
    />
  );
}
