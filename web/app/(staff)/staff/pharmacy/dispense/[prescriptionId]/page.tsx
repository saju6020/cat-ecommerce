'use client';

import { Suspense, use } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { PageHeading } from '@/components/inventory/fields';
import { DispenseScreen } from '@/components/pharmacy/DispenseScreen';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useDispense, useDispensePlan, useRefetchPlan } from '@/lib/hooks/usePharmacy';
import { usePrescription } from '@/lib/hooks/usePrescriptions';

/** `/staff/pharmacy/dispense/[prescriptionId]` — US-PHM-05, 04 AC2: the server's batch plan, FEFO, the witness step. */
function Dispense({ prescriptionId }: { prescriptionId: string }) {
  const repeatId = useSearchParams().get('repeatRequestId');
  const plan = useDispensePlan(prescriptionId);
  const detail = usePrescription(prescriptionId);
  const dispense = useDispense(prescriptionId);
  const refetchPlan = useRefetchPlan(prescriptionId);
  const zone = useBranchTimeZone();
  const repeat = repeatId
    ? detail.data?.repeatRequests.find((r) => r.id === repeatId && r.status === 'Requested')
    : undefined;
  return (
    <div className="flex flex-col gap-4">
      <PageHeading
        title="Dispense"
        actions={
          <Link href="/staff/pharmacy" className="text-sm font-semibold text-accent-ink underline">
            Back to queue
          </Link>
        }
      />
      <QueryBoundary
        query={plan}
        loadingRows={6}
        notFoundTitle="This prescription could not be found"
        notFoundDescription="It may belong to another branch, or the number may be wrong."
      >
        {(data) => (
          <DispenseScreen
            plan={data}
            zone={zone}
            repeatRequest={
              repeat ? { id: repeat.id, note: repeat.note, requestedOn: repeat.requestedOn } : null
            }
            onDispense={dispense}
            onPlanChanged={() => void refetchPlan()}
          />
        )}
      </QueryBoundary>
    </div>
  );
}

export default function DispensePage({ params }: { params: Promise<{ prescriptionId: string }> }) {
  const { prescriptionId } = use(params);
  return (
    <Suspense fallback={null}>
      <Dispense prescriptionId={prescriptionId} />
    </Suspense>
  );
}
