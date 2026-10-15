'use client';

import { useCallback, useState } from 'react';
import { CheckCircle2, CircleDashed } from 'lucide-react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { useCat } from '@/lib/hooks/useCat';
import type { StayCatDetail } from '@/lib/api/types';
import {
  EMPTY_CARE_PROFILE,
  careProfileToForm,
  type CareProfileFormValues,
} from '@/lib/validation/boarding';
import { CareProfileForm, type CareProfileFormProps } from './CareProfileForm';

/**
 * `components/boarding/booking/CareProfileTabs.tsx` — HO-BRD-002 §11.1 step 5: one tab per cat on a
 * single reservation (US-BRD-07 AC1), each with a completeness badge (text + icon) and its own
 * **Save**. Every cat's form stays mounted so switching tabs loses nothing; one
 * `UnsavedChangesGuard` covers all of them (NFR-USA-5). A cat with no saved profile is prefilled
 * from the cat profile's medications and behavioural notes, and says so.
 */
function CatFormLoader({
  cat,
  currencyCode,
  onSave,
  requireAuthorityReason,
  onDirtyChange,
  readOnly,
}: {
  cat: StayCatDetail;
  currencyCode: string;
  onSave: CareProfileFormProps['onSave'];
  requireAuthorityReason?: boolean;
  onDirtyChange: (catId: string, dirty: boolean) => void;
  readOnly?: boolean;
}) {
  const needsPrefill = !cat.careProfile;
  const profile = useCat(cat.catId, { enabled: needsPrefill });

  if (needsPrefill && profile.isPending) {
    return <LoadingState variant="card" rows={5} label={`Loading ${cat.catName}’s details…`} />;
  }

  let initial: CareProfileFormValues = EMPTY_CARE_PROFILE;
  let prefilledNote: string | null = null;
  if (cat.careProfile) {
    initial = careProfileToForm(cat.careProfile);
  } else if (profile.data) {
    const active = profile.data.medications.filter((m) => m.endsOn === null);
    initial = {
      ...EMPTY_CARE_PROFILE,
      medications: active.map((m) => ({
        name: m.medicationName,
        dose: m.dosage,
        route: '',
        times: '',
        instructions: m.frequency,
        fromOn: '',
        toOn: '',
      })),
      notes: profile.data.behaviouralNotes ?? '',
    };
    if (active.length > 0 || profile.data.behaviouralNotes) {
      prefilledNote = `Medication and notes are filled in from ${cat.catName}’s profile`;
    }
  }

  return (
    <CareProfileForm
      catId={cat.catId}
      catName={cat.catName}
      initial={initial}
      prefilledNote={prefilledNote}
      currencyCode={currencyCode}
      onSave={onSave}
      requireAuthorityReason={requireAuthorityReason}
      onDirtyChange={onDirtyChange}
      readOnly={readOnly}
    />
  );
}

export function CareProfileTabs({
  cats,
  currencyCode,
  onSave,
  requireAuthorityReason,
  onContinue,
  onBack,
  readOnly,
  activeCatId,
}: {
  cats: StayCatDetail[];
  currencyCode: string;
  onSave: CareProfileFormProps['onSave'];
  requireAuthorityReason?: boolean;
  onContinue?: () => void;
  onBack?: () => void;
  readOnly?: boolean;
  /** Jump to a named cat's tab (e.g. after `CareProfileIncomplete`). */
  activeCatId?: string | null;
}) {
  const [selected, setSelected] = useState<string>(cats[0]?.catId ?? '');
  const [dirty, setDirty] = useState<Record<string, boolean>>({});
  const current = activeCatId && cats.some((c) => c.catId === activeCatId) ? activeCatId : selected;
  const allComplete = cats.every((c) => c.careProfileComplete);

  const onDirtyChange = useCallback((catId: string, isDirty: boolean) => {
    setDirty((prev) => (prev[catId] === isDirty ? prev : { ...prev, [catId]: isDirty }));
  }, []);

  return (
    <section aria-labelledby="profiles-heading" className="flex flex-col gap-4">
      <UnsavedChangesGuard when={Object.values(dirty).some(Boolean)} />
      <h2 id="profiles-heading" className="font-display text-lg font-semibold text-ink">
        Care profiles
      </h2>
      <div role="tablist" aria-label="Cats" className="flex flex-wrap gap-2">
        {cats.map((cat) => {
          const isCurrent = cat.catId === current;
          return (
            <button
              key={cat.catId}
              role="tab"
              type="button"
              id={`cp-tab-${cat.catId}`}
              aria-selected={isCurrent}
              aria-controls={`cp-panel-${cat.catId}`}
              onClick={() => setSelected(cat.catId)}
              className={`flex min-h-11 items-center gap-2 rounded-[var(--radius-sm)] border px-3 py-2 text-sm font-semibold ${
                isCurrent
                  ? 'border-accent bg-accent-soft text-accent-ink'
                  : 'border-border bg-surface text-ink'
              }`}
            >
              {cat.catName}
              {cat.careProfileComplete ? (
                <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
                  Complete
                </Badge>
              ) : (
                <Badge variant="warning" icon={<CircleDashed size={12} aria-hidden="true" />}>
                  Not finished
                </Badge>
              )}
            </button>
          );
        })}
      </div>
      {cats.map((cat) => (
        <div
          key={cat.catId}
          role="tabpanel"
          id={`cp-panel-${cat.catId}`}
          aria-labelledby={`cp-tab-${cat.catId}`}
          hidden={cat.catId !== current}
          className="flex flex-col gap-4"
        >
          <CatHeaderCompact
            name={cat.catName}
            isHighRisk={cat.catIsHighRisk}
            handlingNote={cat.handlingNote}
          />
          <CatFormLoader
            cat={cat}
            currencyCode={currencyCode}
            onSave={onSave}
            requireAuthorityReason={requireAuthorityReason}
            onDirtyChange={onDirtyChange}
            readOnly={readOnly}
          />
        </div>
      ))}
      {(onContinue || onBack) && (
        <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
          {onBack && (
            <Button variant="secondary" onClick={onBack}>
              Back
            </Button>
          )}
          {onContinue && (
            <>
              <Button onClick={onContinue} disabled={!allComplete}>
                Continue to review
              </Button>
              {!allComplete && (
                <p className="text-sm text-ink-muted">Save every cat’s care profile to continue.</p>
              )}
            </>
          )}
        </div>
      )}
    </section>
  );
}
