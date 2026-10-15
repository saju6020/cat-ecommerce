'use client';

import { useState } from 'react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBillingProfile } from '@/lib/hooks/billing/useBillingQueries';
import { useSetBillingProfile } from '@/lib/hooks/billing/useBillingMutations';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useStaffSession } from '@/lib/hooks/useSession';
import { BillingProfileForm } from './BillingProfileForm';
import { PageHeader } from './PageHeader';
import { SelectField } from './Fields';

/**
 * `components/billing/BillingProfileScreen.tsx` — HO-BIL-002 §11.10, `/staff/admin/billing-profile`.
 * A branch picker (defaulting to the active branch) over `BillingProfileForm`. A branch without a
 * profile (`404`) opens an empty form with a notice; only an administrator can save (the server
 * still decides — a `403` on save reads as a permission message).
 */
export function BillingProfileScreen() {
  const branches = useAdminBranches();
  const activeBranchId = useActiveBranchId();
  const { data: session } = useStaffSession();
  const [picked, setPicked] = useState<string | null>(null);

  const branchId = picked ?? activeBranchId ?? branches.data?.[0]?.id ?? null;
  const profile = useBillingProfile(branchId);
  const save = useSetBillingProfile(branchId ?? '');
  const branchName = branches.data?.find((b) => b.id === branchId)?.name ?? 'This branch';
  const canManage = (session?.roleIds ?? []).includes('Administrator');

  let body;
  if (branches.isLoading || (profile.isLoading && !!branchId)) {
    body = <LoadingState variant="card" rows={6} label="Loading the billing profile…" />;
  } else if (profile.error instanceof ApiForbiddenError || branches.error instanceof ApiForbiddenError) {
    body = <PermissionDeniedState />;
  } else if (profile.isError && !(profile.error instanceof ApiNotFoundError)) {
    body = <ErrorState onRetry={() => void profile.refetch()} />;
  } else if (!branchId) {
    body = <ErrorState title="Choose a branch" description="There is no branch to show a billing profile for." />;
  } else {
    body = (
      <BillingProfileForm
        key={`${branchId}-${profile.data?.version ?? 'none'}`}
        branchName={branchName}
        profile={profile.data ?? null}
        canManage={canManage}
        onSave={(request) => save.mutateAsync(request)}
      />
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Billing profile"
        description="The legal details printed on every invoice and receipt a branch issues."
      />
      {(branches.data?.length ?? 0) > 1 && (
        <SelectField
          id="bp-branch-picker"
          label="Branch"
          wrapperClassName="max-w-xs"
          value={branchId ?? ''}
          onChange={(event) => setPicked(event.target.value)}
        >
          {branches.data?.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </SelectField>
      )}
      {body}
    </div>
  );
}
