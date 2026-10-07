'use client';

import { use } from 'react';
import { VaccinationStatusPanel } from '@/components/cat/VaccinationStatusPanel';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { useCat } from '@/lib/hooks/useCat';
import { useCatVaccinationStatus } from '@/lib/hooks/useCatVaccinationStatus';

/** US-CAT-07 — Current / Due soon / Overdue, with date, booking link and the boarding warning
 *  (HO-CAT-002 §8.3). A dedicated tab, distinct from the Vaccinations tab's record form. */
export default function VaccinationStatusPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const statusQuery = useCatVaccinationStatus(catId);

  if (!catQuery.data) return null;
  const cat = catQuery.data;

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Vaccination status</h2>
      {statusQuery.isLoading ? (
        <LoadingState variant="table" rows={4} />
      ) : statusQuery.isError ? (
        <ErrorState onRetry={() => statusQuery.refetch()} />
      ) : statusQuery.data ? (
        <VaccinationStatusPanel
          status={statusQuery.data}
          bookingHref={`/staff/appointments/new?customerId=${cat.ownerId}&catId=${cat.id}`}
        />
      ) : null}
    </div>
  );
}
