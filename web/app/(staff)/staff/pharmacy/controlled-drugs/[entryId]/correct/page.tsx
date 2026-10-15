'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { CorrectionForm } from '@/components/controlled-drugs/CorrectionForm';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { PageHeading } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useCats } from '@/lib/hooks/useCats';
import { useRecordCorrection, useRegister, useRegisterEntry } from '@/lib/hooks/useControlledDrugs';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';

/**
 * `/staff/pharmacy/controlled-drugs/[entryId]/correct` — FR-PHM-7, C-2: add a correction (a NEW linked
 * entry). Vet only, from a session-roles check before anything is typed, and again by the server.
 */
export default function CorrectEntryPage({ params }: { params: Promise<{ entryId: string }> }) {
  const { entryId } = use(params);
  const router = useRouter();
  const roles = useStaffRoles();
  const zone = useBranchTimeZone();
  const query = useRegisterEntry(entryId);
  const newest = useRegister({ pageNumber: 1, pageSize: 1 });
  const record = useRecordCorrection(entryId);
  const [term, setTerm] = useState('');
  const cats = useCats({ pageSize: 100 });
  const matches = (cats.data?.items ?? []).filter((cat) =>
    cat.name.toLowerCase().includes(term.trim().toLowerCase()),
  );
  if (roles.ready && !roles.has('Vet')) return <PermissionDeniedState />;
  const newestNumber = newest.data?.items[0]?.entryNumber;
  return (
    <div className="flex flex-col gap-4">
      <Link
        href={`/staff/pharmacy/controlled-drugs/${entryId}`}
        className="text-sm font-semibold text-accent-ink underline"
      >
        Back to the entry
      </Link>
      <PageHeading
        title="Add a correction"
        description="A correction is a new entry linked to the original. The original is never changed."
      />
      <QueryBoundary
        query={query}
        loadingRows={5}
        notFoundTitle="This register entry could not be found"
      >
        {(detail) => (
          <CorrectionForm
            original={detail.entry}
            zone={zone}
            nextEntryNumber={newestNumber ? newestNumber + 1 : null}
            cats={term.trim() ? matches.slice(0, 20) : []}
            onSearchCats={setTerm}
            onSubmit={record}
            onDone={(entryNumber) => {
              toast({ title: `Correction added as entry #${entryNumber}` });
              router.push(`/staff/pharmacy/controlled-drugs/${entryId}`);
            }}
          />
        )}
      </QueryBoundary>
    </div>
  );
}
