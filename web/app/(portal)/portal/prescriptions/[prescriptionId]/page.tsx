'use client';

import { Suspense, use } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { MyPrescriptionDetail } from '@/components/my-prescriptions/MyPrescriptionDetail';
import { DEFAULT_BRANCH_TIME_ZONE } from '@/lib/hooks/useBranchTimeZone';
import { useClinicBranches } from '@/lib/hooks/useBooking';
import {
  useMyPrescription,
  useRequestRepeat,
  useWithdrawRepeat,
} from '@/lib/hooks/useMyPrescriptions';

/**
 * `/portal/prescriptions/[prescriptionId]` — US-PHM-06, 07: the directions, the collection history and the
 * repeat request (or why not). `?action=repeat` — the link in the refill reminder — opens the request
 * dialog on load when a repeat is allowed.
 */
function MyPrescription({ id }: { id: string }) {
  const autoOpen = useSearchParams().get('action') === 'repeat';
  const query = useMyPrescription(id);
  const branches = useClinicBranches();
  const request = useRequestRepeat(id);
  const withdraw = useWithdrawRepeat(id);
  return (
    <div className="flex flex-col gap-4">
      <Link
        href="/portal/prescriptions"
        className="text-sm font-semibold text-accent-ink underline"
      >
        All prescriptions
      </Link>
      <QueryBoundary
        query={query}
        loadingRows={5}
        notFoundTitle="We couldn’t find that prescription"
        notFoundDescription="It may not be one of your cats’ prescriptions, or the link may be wrong."
      >
        {(detail) => (
          <MyPrescriptionDetail
            detail={detail}
            zone={branches.data?.[0]?.timeZoneId ?? DEFAULT_BRANCH_TIME_ZONE}
            branches={(branches.data ?? []).map((branch) => ({ id: branch.id, name: branch.name }))}
            autoOpen={autoOpen}
            onRequest={(input) => request.mutateAsync(input)}
            onWithdraw={(requestId) => withdraw.mutateAsync(requestId)}
          />
        )}
      </QueryBoundary>
    </div>
  );
}

export default function MyPrescriptionPage({
  params,
}: {
  params: Promise<{ prescriptionId: string }>;
}) {
  const { prescriptionId } = use(params);
  return (
    <Suspense fallback={null}>
      <MyPrescription id={prescriptionId} />
    </Suspense>
  );
}
