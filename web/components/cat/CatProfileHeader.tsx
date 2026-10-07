import { Cat as CatIcon, Image as ImageIcon } from 'lucide-react';
import type { CatProfile, VaccinationStatus } from '@/lib/api/cats';
import { HighRiskBadge } from './HighRiskBadge';
import { DeceasedBanner } from './DeceasedBanner';
import { AgeDisplay } from './AgeDisplay';
import { VaccinationStatusBadge } from './VaccinationStatusBadge';
import { formatDateOnly } from '@/lib/dateOnly';

/**
 * `components/cat/CatProfileHeader.tsx` — ⭐ HO-CAT-002 §11.3. **The most important component in
 * this slice.** UI-7, US-CAT-03 AC1/AC2, US-CAT-04 AC1. Rendered once from
 * `app/(staff)/staff/cats/[catId]/layout.tsx` (and the portal equivalent) so every child route
 * carries it — see that file for why the placement, not this component, is what makes UI-7 a
 * guarantee rather than a convention.
 *
 * Visual priority, top to bottom, matches §11.3 exactly:
 *  1. High-risk warning (before the name — component test U6)
 *  2. Deceased banner, with the date (U8)
 *  3. Photo / name / species / breed / colour
 *  4. Age (approximate when estimated)
 *  5. Sex and neuter status
 *  6. Latest weight, its unit always visible (U9, UI-8)
 *  7. Allergies — chips, or the literal "None recorded" (U4/U5 — US-CAT-03 AC2: never blank)
 *  8. Chronic conditions — same treatment
 *  9. Vaccination status badge, when known
 *
 * `vaccinationStatus` is a prop, not fetched here — it comes from a different query
 * (`GET .../vaccination-status`, Q4) than the profile itself, and components don't fetch
 * (HO-CAT-002 §8.2 rule 4); the page/layout composing this one owns both queries.
 *
 * No real photo renders — HO-CAT-001 §4.2 / HO-CAT-002 §11.6: the API stores a storage key, not
 * bytes, so there is nothing to point an `<img>` at yet. A neutral placeholder icon stands in;
 * this is not a bug to "fix" by inventing an image URL.
 */
export interface CatProfileHeaderProps {
  cat: CatProfile;
  vaccinationStatus?: VaccinationStatus;
}

export function CatProfileHeader({ cat, vaccinationStatus }: CatProfileHeaderProps) {
  const hasPhoto = cat.photos.length > 0;

  return (
    <header className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-6">
      {cat.isHighRisk && <HighRiskBadge handlingNote={cat.handlingNote} />}
      {cat.lifeStatus === 'Deceased' && (
        <DeceasedBanner deceasedOn={cat.deceasedOn} deceasedNote={cat.deceasedNote} />
      )}

      <div className="flex flex-wrap items-start gap-5">
        <div
          aria-hidden="true"
          className="flex h-20 w-20 flex-none items-center justify-center rounded-full border border-border bg-surface-sunken text-ink-faint"
        >
          {hasPhoto ? <ImageIcon size={28} /> : <CatIcon size={28} />}
        </div>

        <div className="min-w-[220px] flex-1">
          <h1 className="font-display text-2xl font-semibold text-ink">{cat.name}</h1>
          <p className="text-sm text-ink-muted">
            {cat.species}
            {cat.breed ? ` · ${cat.breed}` : ''}
            {cat.colourAndMarkings ? ` · ${cat.colourAndMarkings}` : ''}
          </p>

          <dl className="mt-3 grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Age</dt>
              <dd className="text-ink">
                <AgeDisplay
                  dateOfBirth={cat.dateOfBirth}
                  dateOfBirthPrecision={cat.dateOfBirthPrecision}
                />
              </dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Sex</dt>
              <dd className="text-ink">
                {cat.sex}
                {cat.neuterStatus !== 'Unknown' ? ` · ${cat.neuterStatus}` : ''}
                {cat.neuterStatus === 'Neutered' && cat.neuteredOn
                  ? ` (${formatDateOnly(cat.neuteredOn)})`
                  : ''}
              </dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
                Latest weight
              </dt>
              <dd className="text-ink">
                {cat.latestWeightKg != null ? `${cat.latestWeightKg} kg` : 'Not recorded'}
              </dd>
            </div>
          </dl>
        </div>

        {vaccinationStatus && (
          <div className="flex-none">
            <VaccinationStatusBadge status={vaccinationStatus} />
          </div>
        )}
      </div>

      <div className="grid gap-4 border-t border-border pt-4 sm:grid-cols-2">
        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            Allergies
          </h2>
          {cat.hasRecordedAllergies ? (
            <ul className="mt-1.5 flex flex-wrap gap-1.5">
              {cat.allergies.map((allergy) => (
                <li key={allergy.id}>
                  <span className="inline-flex items-center rounded-full bg-warning-bg px-2.5 py-1 text-xs font-semibold text-warning-ink">
                    {allergy.allergen}
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-1.5 text-sm text-ink-muted">None recorded</p>
          )}
        </div>

        <div>
          <h2 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            Chronic conditions
          </h2>
          {cat.chronicConditions.length > 0 ? (
            <ul className="mt-1.5 flex flex-wrap gap-1.5">
              {cat.chronicConditions.map((condition) => (
                <li key={condition.id}>
                  <span className="inline-flex items-center rounded-full bg-surface-sunken px-2.5 py-1 text-xs font-semibold text-ink-muted">
                    {condition.condition}
                  </span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-1.5 text-sm text-ink-muted">None recorded</p>
          )}
        </div>
      </div>
    </header>
  );
}
