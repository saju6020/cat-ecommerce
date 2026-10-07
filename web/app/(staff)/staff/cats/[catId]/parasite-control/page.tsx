'use client';

import { use, useState } from 'react';
import {
  ParasiteTreatmentForm,
  type ParasiteTreatmentFormValues,
} from '@/components/cat/ParasiteTreatmentForm';
import { DeceasedBanner } from '@/components/cat/DeceasedBanner';
import { useCat } from '@/lib/hooks/useCat';
import { useRecordCatParasiteTreatment } from '@/lib/hooks/useCatMutations';
import { toast } from '@/components/ui/use-toast';
import { formatDateOnly } from '@/lib/dateOnly';
import type { ParasiteTreatmentType } from '@/lib/api/cats';

/**
 * Parasite control — US-CAT-06 (HO-CAT-002 §8.3).
 *
 * **A11 (HO-CAT-002 §18, U33):** "records and shows its next-due date". HO-CAT-001's C10
 * (`POST .../parasite-treatments`) is amended (owner decision 2026-09-25, HO-CAT-001 §1) so its
 * `201` body carries the created treatment's `nextDueOn`, computed by
 * `CatAggregate.RecordParasiteTreatment` (HO-CAT-001 §8.2) — the single source of truth for that
 * rule. This page renders exactly the value the server returned, as visible, accessible text
 * (`role="status"`, not only the toast below it), and never recomputes
 * `administeredOn + intervalDays` itself; doing that in the browser would be a second
 * implementation of the aggregate rule, which the ARCHITECTURE gate already rejected once for the
 * analogous vaccination case (fix `1a4cac4`). `nextDueOn` is required, not nullable (owner
 * confirmed 2026-09-25) — every recorded treatment has one.
 *
 * `lastRecorded` is local, ephemeral UI state (HO-CAT-002 §8.4), not server data, so
 * nothing keys this component by `catId` the way `useCat`'s query key does — a client-side
 * navigation from one cat's `/parasite-control` tab to another's reuses the same component
 * instance. So the state remembers which cat it belongs to, and the banner renders only for that
 * cat: cat A's next-due date can never be shown on cat B's page, not even for one frame when cat
 * B's profile is already cached (CODE_REVIEW gate HIGH, third pass).
 */
export default function ParasiteControlPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  const catQuery = useCat(catId);
  const recordTreatment = useRecordCatParasiteTreatment(catId);
  const [lastRecorded, setLastRecorded] = useState<{ catId: string; nextDueOn: string }>();
  const lastRecordedNextDueOn = lastRecorded?.catId === catId ? lastRecorded.nextDueOn : undefined;

  if (!catQuery.data) return null;
  const cat = catQuery.data;
  const isDeceased = cat.lifeStatus === 'Deceased';

  async function handleSubmit(values: ParasiteTreatmentFormValues) {
    const response = await recordTreatment.mutateAsync({
      treatmentType: values.treatmentType as ParasiteTreatmentType,
      productName: values.productName,
      batchReference: values.batchReference || null,
      administeredOn: values.administeredOn,
      administeredByStaffId: values.administeredByStaffId,
      intervalDays: values.intervalDays,
      notes: values.notes || null,
    });
    setLastRecorded({ catId, nextDueOn: response.nextDueOn });
    toast({ title: 'Treatment recorded' });
  }

  return (
    <div className="flex flex-col gap-6">
      <h2 className="text-lg font-bold text-ink">Parasite control</h2>
      {isDeceased && <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />}
      {!isDeceased && (
        <ParasiteTreatmentForm onSubmit={handleSubmit} isSubmitting={recordTreatment.isPending} />
      )}
      {lastRecordedNextDueOn !== undefined && (
        <div
          role="status"
          className="rounded-[var(--radius-md)] border border-border bg-surface p-4"
        >
          <p className="text-sm font-semibold text-ink">Treatment recorded</p>
          <p className="text-sm text-ink-muted">{`Next due ${formatDateOnly(lastRecordedNextDueOn)}`}</p>
        </div>
      )}
    </div>
  );
}
