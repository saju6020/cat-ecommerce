'use client';

import { Cat as CatIcon } from 'lucide-react';
import { HighRiskBadge } from '@/components/cat/HighRiskBadge';
import { VaccinationStatusBadge } from '@/components/cat/VaccinationStatusBadge';
import { AgeDisplay } from '@/components/cat/AgeDisplay';
import { LoadingState } from '@/components/common/LoadingState';
import { useCat } from '@/lib/hooks/useCat';
import { useCatVaccinationStatus } from '@/lib/hooks/useCatVaccinationStatus';
import { formatMeasurement } from '@/lib/format/clinical';

/**
 * `VisitCatHeader` — HO-EMR-002 UI-7's COMPACT cat header for the workspace, the vitals dialog, the
 * summary and the consent screen. HO-CAT-002's `CatProfileHeader` is the full one (still used on the
 * cat's own pages); this condenses the same safety facts — the high-risk strip FIRST, the name, age,
 * sex, latest weight WITH its unit, allergies and chronic conditions (written out as "None recorded"
 * when empty, never blank) and the vaccination badge — so they are never off screen during a
 * consultation. Loading and failure are stated: a missing header must not read as a cat with no alerts.
 */
export function VisitCatHeader({ catId }: { catId: string }) {
  const cat = useCat(catId);
  const vaccination = useCatVaccinationStatus(catId);

  if (cat.isLoading)
    return <LoadingState variant="card" rows={2} label="Loading the cat’s details…" />;
  if (!cat.data) {
    return (
      <p
        role="status"
        className="rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm text-ink-muted"
      >
        The cat’s details couldn’t be loaded, so allergies and alerts are not shown here.
      </p>
    );
  }
  const data = cat.data;
  return (
    <section
      aria-label={`Cat — ${data.name}`}
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      {data.isHighRisk && <HighRiskBadge handlingNote={data.handlingNote} />}
      <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
        <div className="flex min-w-0 items-center gap-2">
          <CatIcon size={20} className="flex-none text-ink-muted" aria-hidden="true" />
          <span className="font-display text-lg font-semibold text-ink">{data.name}</span>
        </div>
        <p className="text-sm text-ink-muted">
          {data.breed ?? data.species} · {data.sex} ·{' '}
          <AgeDisplay
            dateOfBirth={data.dateOfBirth}
            dateOfBirthPrecision={data.dateOfBirthPrecision}
          />
        </p>
        <p className="text-sm text-ink-muted">
          Latest weight{' '}
          <strong className="text-ink">
            {data.latestWeightKg != null
              ? formatMeasurement({ value: data.latestWeightKg.toFixed(2), unit: 'kg' })
              : 'Not recorded'}
          </strong>
        </p>
        {vaccination.data && <VaccinationStatusBadge status={vaccination.data.overallStatus} />}
      </div>
      <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            Allergies
          </dt>
          <dd className="text-ink">
            {data.hasRecordedAllergies && data.allergies.length > 0
              ? data.allergies.map((a) => a.allergen).join(', ')
              : 'None recorded'}
          </dd>
        </div>
        <div>
          <dt className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            Chronic conditions
          </dt>
          <dd className="text-ink">
            {data.chronicConditions.length > 0
              ? data.chronicConditions.map((c) => c.condition).join(', ')
              : 'None recorded'}
          </dd>
        </div>
      </dl>
    </section>
  );
}
