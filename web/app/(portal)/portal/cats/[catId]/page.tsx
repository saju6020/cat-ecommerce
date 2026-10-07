'use client';

import { use } from 'react';
import Link from 'next/link';
import { VaccinationStatusPanel } from '@/components/cat/VaccinationStatusPanel';
import { LoadingState } from '@/components/common/LoadingState';
import { buttonVariants } from '@/components/ui/button';
import { useCat } from '@/lib/hooks/useCat';
import { useCatVaccinationStatus } from '@/lib/hooks/useCatVaccinationStatus';

/** Portal cat profile — US-CAT-07 AC2 (HO-CAT-002 §8.3): "Due soon" shows a booking link,
 *  US-CAT-10's PDF download is one click away via the Medical history tab. */
export default function PortalCatProfilePage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const statusQuery = useCatVaccinationStatus(catId);

  if (!catQuery.data) return null;

  return (
    <div className="flex flex-col gap-6">
      <section>
        <h2 className="text-lg font-bold text-ink">Vaccination status</h2>
        {statusQuery.isLoading ? (
          <LoadingState variant="table" rows={3} />
        ) : statusQuery.data ? (
          <VaccinationStatusPanel status={statusQuery.data} bookingHref="" bookingDisabled />
        ) : null}
      </section>

      <Link
        href={`/portal/cats/${catId}/food`}
        className={buttonVariants({ variant: 'secondary' })}
      >
        Food suggestions
      </Link>

      <Link
        href={`/portal/cats/${catId}/medical-history`}
        className={buttonVariants({ variant: 'secondary' })}
      >
        View medical history
      </Link>
    </div>
  );
}
