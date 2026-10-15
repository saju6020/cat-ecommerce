'use client';

import { Suspense, use } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { PrescriptionComposer } from '@/components/prescriptions/PrescriptionComposer';
import { isGuid } from '@/lib/validation/guid';

/**
 * `/staff/cats/[catId]/prescriptions/new?visitId=` — US-PHM-01, 02, 03 (ADR-FE-059). The composer under
 * the cat's clinical header, so the allergies stay on screen while prescribing. The composer never
 * navigates on its own; this page does, to the new prescription's detail.
 */
function NewPrescription({ catId }: { catId: string }) {
  const router = useRouter();
  const visitParam = useSearchParams().get('visitId');
  const visitId = visitParam && isGuid(visitParam) ? visitParam : null;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-ink">New prescription</h2>
        <Link
          href={`/staff/cats/${catId}/prescriptions`}
          className="text-sm font-semibold text-accent-ink underline"
        >
          Back to prescriptions
        </Link>
      </div>
      <PrescriptionComposer
        catId={catId}
        visitId={visitId}
        onIssued={(prescriptionId) =>
          router.push(`/staff/cats/${catId}/prescriptions/${prescriptionId}`)
        }
      />
    </div>
  );
}

export default function NewPrescriptionPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  return (
    <Suspense fallback={null}>
      <NewPrescription catId={catId} />
    </Suspense>
  );
}
