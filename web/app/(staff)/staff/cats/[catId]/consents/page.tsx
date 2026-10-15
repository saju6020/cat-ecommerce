'use client';

import { use } from 'react';
import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ConsentList } from '@/components/consent/ConsentList';
import { ApiForbiddenError } from '@/lib/api/client';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useCatConsents } from '@/lib/hooks/useConsents';

/** `/staff/cats/[catId]/consents` — HO-EMR-002 §8.1/§11.6 (US-CLN-21): the cat's consents, under the cat layout. */
export default function CatConsentsPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const consents = useCatConsents(catId);
  const zone = useBranchTimeZone();
  if (consents.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-lg font-bold text-ink">Consents</h2>
      <p className="text-sm text-ink-muted">
        To record a new consent, open the procedure’s appointment and choose{' '}
        <strong>Record consent</strong> (
        <Link
          href="/staff/appointments"
          className="font-semibold text-accent-ink underline underline-offset-2"
        >
          find the appointment
        </Link>
        ).
      </p>
      <ConsentList
        rows={consents.data}
        zone={zone}
        isLoading={consents.isLoading}
        isError={consents.isError}
        onRetry={() => void consents.refetch()}
      />
    </div>
  );
}
