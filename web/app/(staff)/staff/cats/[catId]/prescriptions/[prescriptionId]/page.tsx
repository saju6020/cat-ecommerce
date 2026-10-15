'use client';

import { use } from 'react';
import Link from 'next/link';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { PrescriptionDetail } from '@/components/prescriptions/PrescriptionDetail';
import { toast } from '@/components/ui/use-toast';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import {
  useCancelPrescription,
  useDeclineRepeat,
  usePrescription,
} from '@/lib/hooks/usePrescriptions';

/** `/staff/cats/[catId]/prescriptions/[prescriptionId]` — US-PHM-01, 06: detail, cancel, renew, decline a repeat. */
export default function PrescriptionPage({
  params,
}: {
  params: Promise<{ catId: string; prescriptionId: string }>;
}) {
  const { catId, prescriptionId } = use(params);
  const query = usePrescription(prescriptionId);
  const zone = useBranchTimeZone();
  const cancel = useCancelPrescription(prescriptionId, catId);
  const decline = useDeclineRepeat(prescriptionId, catId);
  return (
    <div className="flex flex-col gap-4">
      <Link
        href={`/staff/cats/${catId}/prescriptions`}
        className="text-sm font-semibold text-accent-ink underline"
      >
        All prescriptions
      </Link>
      <QueryBoundary
        query={query}
        loadingRows={6}
        notFoundTitle="This prescription could not be found"
        notFoundDescription="It may belong to another branch, or the link may be wrong."
      >
        {(detail) => (
          <PrescriptionDetail
            detail={detail}
            zone={zone}
            catId={catId}
            onCancel={async (reason) => {
              await cancel.mutateAsync(reason);
              toast({ title: `${detail.prescriptionNumber} cancelled` });
            }}
            onDeclineRepeat={async (requestId, reason) => {
              await decline.mutateAsync({ requestId, reason });
              toast({ title: 'Repeat request declined' });
            }}
          />
        )}
      </QueryBoundary>
    </div>
  );
}
