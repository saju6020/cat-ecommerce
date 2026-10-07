'use client';

import { useRouter } from 'next/navigation';
import { BranchDetailsForm } from '@/components/branches/BranchDetailsForm';
import { useCreateBranch } from '@/lib/hooks/useAdminBranches';

/** `/staff/admin/branches/new` — US-ADM-01. */
export default function NewBranchPage() {
  const router = useRouter();
  const createMutation = useCreateBranch();

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">New branch</h1>
      <BranchDetailsForm
        isPending={createMutation.isPending}
        onSave={async (values) => {
          const result = await createMutation.mutateAsync({
            code: values.code!,
            name: values.name,
            timeZoneId: values.timeZoneId,
            addressLine: values.addressLine,
            phoneNumber: values.phoneNumber,
            email: values.email,
          });
          router.push(`/staff/admin/branches/${result.branchId}`);
        }}
      />
    </div>
  );
}
