'use client';

import { StaffAccountForm } from '@/components/admin/StaffAccountForm';
import { useStaffRoleCatalogue } from '@/lib/hooks/useStaffAdmin';
import { useCreateStaffAccount } from '@/lib/hooks/useStaffAdminMutations';

// US-ACC-07.
// ARCH gate HIGH 3 (first pass) — this page wires useStaffRoleCatalogue()/useCreateStaffAccount();
// StaffAccountForm itself takes the results as props.
export default function NewStaffAccountPage() {
  const catalogue = useStaffRoleCatalogue();
  const mutation = useCreateStaffAccount();

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">New staff account</h1>
      <StaffAccountForm
        catalogue={catalogue}
        onCreate={(request) => mutation.mutateAsync(request)}
        isPending={mutation.isPending}
      />
    </div>
  );
}
