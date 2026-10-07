'use client';

import { use } from 'react';
import Link from 'next/link';
import { VaccinationForm, type VaccinationFormValues } from '@/components/cat/VaccinationForm';
import { VaccinationStatusPanel } from '@/components/cat/VaccinationStatusPanel';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { LoadingState } from '@/components/common/LoadingState';
import { useCat } from '@/lib/hooks/useCat';
import { useCatVaccinationStatus } from '@/lib/hooks/useCatVaccinationStatus';
import { useAllVaccines } from '@/lib/hooks/useVaccines';
import { useRecordCatVaccination } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import { getCatMedicalHistoryPdfUrl } from '@/lib/api/cats';

/**
 * Vaccination record + status — US-CAT-05, US-CAT-07 (HO-CAT-002 §8.3).
 *
 * **Contract gap, recorded rather than worked around (HO-CAT-002 §20.2):** HO-CAT-001 has no
 * `GET /api/cats/{catId}/vaccinations` — only `POST .../vaccinations` (record one) and
 * `GET .../vaccination-status` (Q4: the *current* status per core vaccine, not a chronological
 * list of every vaccination with its batch and vet). So this page can record a vaccination and
 * show the status panel it immediately affects, but a full per-record history table (batch
 * reference, administering vet, one row per past vaccination) isn't buildable from the read side
 * today — the medical-history PDF (US-CAT-10) is the only place that detail is rendered. Do not
 * invent a list endpoint to fill this gap (§20.2) — it's reported as a finding against
 * HO-CAT-001 instead.
 */
export default function VaccinationsPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const statusQuery = useCatVaccinationStatus(catId);
  const vaccinesQuery = useAllVaccines({ activeOnly: true });
  const recordVaccination = useRecordCatVaccination(catId);

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  async function handleSubmit(values: VaccinationFormValues) {
    await recordVaccination.mutateAsync({
      vaccineId: values.vaccineId,
      batchReference: values.batchReference,
      batchExpiresOn: values.batchExpiresOn,
      administeredOn: values.administeredOn,
      administeredByVetId: values.administeredByVetId,
      notes: values.notes || null,
    });
    toast({ title: 'Vaccination recorded' });
  }

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Vaccinations</h2>
      {isDeceased && <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />}

      {!isDeceased && (
        <section className="flex flex-col gap-3">
          <h3 className="text-sm font-bold text-ink">Record a vaccination</h3>
          {vaccinesQuery.isLoading ? (
            <LoadingState variant="card" rows={2} />
          ) : (
            <VaccinationForm
              vaccines={vaccinesQuery.data ?? []}
              onSubmit={handleSubmit}
              isSubmitting={recordVaccination.isPending}
            />
          )}
        </section>
      )}

      <section className="flex flex-col gap-3">
        <h3 className="text-sm font-bold text-ink">Current status</h3>
        {statusQuery.isLoading ? (
          <LoadingState variant="table" rows={3} />
        ) : statusQuery.data ? (
          <VaccinationStatusPanel
            status={statusQuery.data}
            bookingHref={`/staff/appointments/new?customerId=${cat.ownerId}&catId=${cat.id}`}
          />
        ) : null}
        <p className="text-sm text-ink-muted">
          For the full history of every vaccination given (batch, administering vet), see the{' '}
          <Link
            href={getCatMedicalHistoryPdfUrl(catId)}
            className="font-semibold text-accent-ink underline"
          >
            medical history PDF
          </Link>
          .
        </p>
      </section>
    </div>
  );
}
