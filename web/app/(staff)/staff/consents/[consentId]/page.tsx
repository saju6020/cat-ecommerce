'use client';

import { use } from 'react';
import Link from 'next/link';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { ConsentDetail } from '@/components/consent/ConsentDetail';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useConsent, useWithdrawConsent } from '@/lib/hooks/useConsents';

/** `/staff/consents/[consentId]` — HO-EMR-002 §8.1/§11.6 (US-CLN-21): what was signed, and withdrawal. */
export default function ConsentDetailPage({ params }: { params: Promise<{ consentId: string }> }) {
  const { consentId } = use(params);
  const query = useConsent(consentId);
  const withdraw = useWithdrawConsent(consentId, query.data?.catId ?? '');

  if (query.isLoading) return <LoadingState variant="card" rows={6} label="Loading the consent…" />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t find this consent."
        description="It may belong to another branch."
      />
    );
  }
  if (query.isError || !query.data)
    return <ErrorState headingLevel={1} onRetry={() => void query.refetch()} />;

  return (
    <div className="flex flex-col gap-5">
      <Link
        href={`/staff/cats/${query.data.catId}/consents`}
        className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}
      >
        Back to the cat’s consents
      </Link>
      <VisitCatHeader catId={query.data.catId} />
      <ConsentDetail
        consent={query.data}
        canWithdraw
        onWithdraw={async (reason) => {
          await withdraw.mutateAsync(reason);
          toast({ title: 'Consent withdrawn' });
        }}
      />
    </div>
  );
}
