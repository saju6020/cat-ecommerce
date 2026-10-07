'use client';

import { use } from 'react';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useAdminPolicies, usePublishPolicyVersion } from '@/lib/hooks/useAdminContent';
import { PolicyDetail } from '@/components/content/PolicyDetail';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/policies/[key]` — US-ADM-07. */
export default function PolicyDetailPage({ params }: { params: Promise<{ key: string }> }) {
  const { key } = use(params);
  const { data: session } = useStaffSession();
  const { data: policies, isLoading, error } = useAdminPolicies();
  const publishMutation = usePublishPolicyVersion(key);

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (isLoading) return <LoadingState rows={5} />;

  const policy = policies?.find((p) => p.key === key);
  if (!policy)
    return (
      <ErrorState
        title="Policy not found"
        description="This policy document may have been removed."
      />
    );

  return (
    <PolicyDetail
      policy={policy}
      canManage={(session?.roleIds ?? []).includes('Administrator')}
      isPending={publishMutation.isPending}
      onPublishVersion={(request) => publishMutation.mutateAsync(request)}
    />
  );
}
