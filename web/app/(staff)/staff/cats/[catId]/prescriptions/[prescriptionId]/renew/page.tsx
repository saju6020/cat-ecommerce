'use client';

import { Suspense, use } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { PrescriptionComposer } from '@/components/prescriptions/PrescriptionComposer';
import { isGuid } from '@/lib/validation/guid';

/**
 * `/staff/cats/[catId]/prescriptions/[prescriptionId]/renew` — US-PHM-06 AC2. The composer prefilled
 * from the prescription being renewed. `?visitId=` is optional: a renewal needs no open visit (remote
 * renewal — owner decision 2026-10-10). Every check runs again on today's weight and records.
 */
function Renew({ catId, prescriptionId }: { catId: string; prescriptionId: string }) {
  const router = useRouter();
  const visitParam = useSearchParams().get('visitId');
  const visitId = visitParam && isGuid(visitParam) ? visitParam : null;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-ink">Renew prescription</h2>
        <Link
          href={`/staff/cats/${catId}/prescriptions/${prescriptionId}`}
          className="text-sm font-semibold text-accent-ink underline"
        >
          Back to the prescription
        </Link>
      </div>
      <PrescriptionComposer
        catId={catId}
        visitId={visitId}
        renewFrom={prescriptionId}
        onIssued={(newId) => router.push(`/staff/cats/${catId}/prescriptions/${newId}`)}
      />
    </div>
  );
}

export default function RenewPage({
  params,
}: {
  params: Promise<{ catId: string; prescriptionId: string }>;
}) {
  const { catId, prescriptionId } = use(params);
  return (
    <Suspense fallback={null}>
      <Renew catId={catId} prescriptionId={prescriptionId} />
    </Suspense>
  );
}
