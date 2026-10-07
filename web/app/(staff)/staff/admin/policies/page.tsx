'use client';

import { useStaffSession } from '@/lib/hooks/useSession';
import { useAdminPolicies, useCreatePolicyDocument } from '@/lib/hooks/useAdminContent';
import { PolicyList } from '@/components/content/PolicyList';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError } from '@/lib/api/client';

/** `/staff/admin/policies` — US-ADM-07. List is `Content.Manage`; "New policy" is
 *  `Policies.Manage` (Administrator only, HO-ADM-001 Amended (28)) — presentation only. */
export default function PoliciesPage() {
  const { data: session } = useStaffSession();
  const { data, isLoading, error } = useAdminPolicies();
  const createMutation = useCreatePolicyDocument();

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (isLoading) return <LoadingState rows={5} />;

  return (
    <PolicyList
      policies={data}
      canManage={(session?.roleIds ?? []).includes('Administrator')}
      isPending={createMutation.isPending}
      onCreate={(request) => createMutation.mutateAsync(request)}
    />
  );
}
