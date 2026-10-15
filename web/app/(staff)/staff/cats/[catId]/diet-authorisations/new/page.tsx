'use client';

import { Suspense, use } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { DietAuthorisationForm } from '@/components/diet-authorisations/DietAuthorisationForm';
import { isGuid } from '@/lib/validation/guid';

/** `/staff/cats/[catId]/diet-authorisations/new?visitId=` — US-PHM-08: authorise a diet from an open visit. */
function Authorise({ catId }: { catId: string }) {
  const router = useRouter();
  const visitParam = useSearchParams().get('visitId');
  const visitId = visitParam && isGuid(visitParam) ? visitParam : null;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-bold text-ink">Authorise a diet</h2>
        <Link
          href={`/staff/cats/${catId}/diet-authorisations`}
          className="text-sm font-semibold text-accent-ink underline"
        >
          Back to diet authorisations
        </Link>
      </div>
      <DietAuthorisationForm
        catId={catId}
        visitId={visitId}
        onAuthorised={() => router.push(`/staff/cats/${catId}/diet-authorisations`)}
      />
    </div>
  );
}

export default function AuthorisePage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  return (
    <Suspense fallback={null}>
      <Authorise catId={catId} />
    </Suspense>
  );
}
