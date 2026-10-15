'use client';

import { use } from 'react';
import Link from 'next/link';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { RegisterEntryDetail } from '@/components/controlled-drugs/RegisterEntryDetail';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useRegisterEntry } from '@/lib/hooks/useControlledDrugs';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

/** `/staff/pharmacy/controlled-drugs/[entryId]` — FR-PHM-7: the entry and its correction chain (read-only; vets may add a correction). */
export default function RegisterEntryPage({ params }: { params: Promise<{ entryId: string }> }) {
  const { entryId } = use(params);
  const query = useRegisterEntry(entryId);
  const zone = useBranchTimeZone();
  const roles = useStaffRoles();
  return (
    <div className="flex flex-col gap-4">
      <Link
        href="/staff/pharmacy/controlled-drugs"
        className="text-sm font-semibold text-accent-ink underline"
      >
        Back to the register
      </Link>
      <QueryBoundary
        query={query}
        loadingRows={6}
        notFoundTitle="This register entry could not be found"
        notFoundDescription="It may belong to another branch, or the link may be wrong."
      >
        {(detail) => (
          <RegisterEntryDetail detail={detail} zone={zone} canCorrect={roles.has('Vet')} />
        )}
      </QueryBoundary>
    </div>
  );
}
