'use client';

import { useRef, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { FileUploader } from '@/components/files/FileUploader';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import {
  FieldError,
  FormErrorAlert,
  SelectRow,
  TextAreaRow,
} from '@/components/boarding/shared/FormBits';
import { GateStatusBadge } from '@/components/boarding/shared/StatusBadges';
import { BELONGING_KIND_LABEL } from '@/components/boarding/shared/labels';
import { SignaturePad } from '@/components/signature/SignaturePad';
import { ApiValidationError } from '@/lib/api/client';
import { describeBoardingError, parseChecklistProperty } from '@/lib/api/errors';
import type { BelongingKind, StaffStayDetail } from '@/lib/api/types';
import type { CheckInCatInput } from '@/lib/api/staff-boarding';
import { useCheckInStay } from '@/lib/hooks/useStaffBoarding';
import {
  EMPTY_CHECK_IN_CAT,
  checkInCatErrors,
  draftToSignature,
  type ChecklistErrors,
  type CheckInCatDraft,
} from '@/lib/validation/boarding';

const FIELD_FROM_SERVER: Record<string, string> = {
  identityConfirmed: 'identityConfirmed',
  vaccinationProofVerified: 'vaccinationProofVerified',
  weightKg: 'weightKg',
  arrivalHealthObservation: 'arrivalHealthObservation',
  belongings: 'belongings',
  ownerSignature: 'signature',
};

function toInput(catId: string, d: CheckInCatDraft): CheckInCatInput {
  return {
    catId,
    identityConfirmed: d.identityConfirmed,
    vaccinationProofVerified: d.vaccinationProofVerified,
    weightKg: Number(d.weightKg),
    arrivalHealthObservation: d.arrivalHealthObservation.trim(),
    noBelongings: d.noBelongings,
    belongings: d.noBelongings
      ? []
      : d.belongings.map((b) => ({
          kind: b.kind as BelongingKind,
          description: b.description.trim(),
          fileIds: b.fileIds,
        })),
    ownerSignature: draftToSignature(d.signature),
  };
}

/**
 * `components/boarding/stay/CheckInWizard.tsx` — ⭐ HO-BRD-002 §11.8, US-BRD-08, ADR-FE-108. Per-cat
 * steps (identity, **vaccination proof**, weight with its unit always visible, arrival health
 * observation, belongings, owner signature) and a summary; **Check in submits once**. If the server
 * answers `CheckInChecklistIncomplete` each missing item is mapped back onto its cat step and field
 * and the wizard jumps to the first one — nothing already entered is lost. A cat whose vaccination
 * the server holds is flagged (the override reserves a unit, it does not admit a cat).
 */
export function CheckInWizard({ stay, onDone }: { stay: StaffStayDetail; onDone: () => void }) {
  const checkIn = useCheckInStay(stay.id);
  const [drafts, setDrafts] = useState<CheckInCatDraft[]>(() =>
    stay.cats.map(() => ({ ...EMPTY_CHECK_IN_CAT })),
  );
  const [step, setStep] = useState(0);
  const [errors, setErrors] = useState<ChecklistErrors[]>(() => stay.cats.map(() => ({})));
  const [serverError, setServerError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const submitting = useRef(false);
  const summaryStep = stay.cats.length;

  function patch(index: number, next: Partial<CheckInCatDraft>) {
    setDirty(true);
    setDrafts((prev) => prev.map((d, i) => (i === index ? { ...d, ...next } : d)));
  }

  function next() {
    const found = checkInCatErrors(drafts[step]!);
    setErrors((prev) => prev.map((e, i) => (i === step ? found : e)));
    if (Object.keys(found).length === 0) setStep(step + 1);
  }

  async function submit() {
    if (submitting.current) return;
    const all = drafts.map((d) => checkInCatErrors(d));
    setErrors(all);
    const firstBad = all.findIndex((e) => Object.keys(e).length > 0);
    if (firstBad >= 0) {
      setStep(firstBad);
      return;
    }
    submitting.current = true;
    setServerError(null);
    try {
      await checkIn.mutateAsync(stay.cats.map((c, i) => toInput(c.catId, drafts[i]!)));
      setDirty(false);
      // the owner's signature is not kept in memory once the check-in is recorded
      setDrafts(stay.cats.map(() => ({ ...EMPTY_CHECK_IN_CAT })));
      checkIn.reset();
      onDone();
    } catch (e) {
      if (e instanceof ApiValidationError) {
        const mapped: ChecklistErrors[] = stay.cats.map(() => ({}));
        let first = -1;
        for (const v of e.response.errors) {
          const parsed = parseChecklistProperty(v.propertyName);
          if (parsed && mapped[parsed.catIndex]) {
            const field = FIELD_FROM_SERVER[parsed.field] ?? 'general';
            mapped[parsed.catIndex]![field] = v.errorMessage;
            if (first < 0 || parsed.catIndex < first) first = parsed.catIndex;
          }
        }
        setErrors(mapped);
        if (first >= 0) setStep(first);
      }
      setServerError(describeBoardingError(e));
    } finally {
      submitting.current = false;
    }
  }

  const cat = stay.cats[step];
  const draft = drafts[step];
  const err = errors[step] ?? {};

  return (
    <section
      aria-labelledby="checkin-heading"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-5"
    >
      <UnsavedChangesGuard when={dirty} />
      <h2 id="checkin-heading" className="font-display text-lg font-semibold text-ink">
        Check in
      </h2>
      <ol aria-label="Check-in steps" className="flex flex-wrap gap-2 text-[12.5px] font-semibold">
        {stay.cats.map((c, i) => (
          <li
            key={c.catId}
            aria-current={step === i ? 'step' : undefined}
            className={`rounded-full px-3 py-1 ${step === i ? 'bg-accent text-white' : Object.keys(errors[i] ?? {}).length > 0 ? 'bg-danger-bg text-danger-ink' : 'bg-surface-sunken text-ink-muted'}`}
          >
            {c.catName}
            {Object.keys(errors[i] ?? {}).length > 0 ? ' (needs attention)' : ''}
          </li>
        ))}
        <li
          aria-current={step === summaryStep ? 'step' : undefined}
          className={`rounded-full px-3 py-1 ${step === summaryStep ? 'bg-accent text-white' : 'bg-surface-sunken text-ink-muted'}`}
        >
          Summary
        </li>
      </ol>
      <FormErrorAlert message={serverError} />

      {cat && draft && (
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-3">
            <CatHeaderCompact
              name={cat.catName}
              isHighRisk={cat.catIsHighRisk}
              handlingNote={cat.handlingNote}
            />
            <GateStatusBadge status={cat.gateStatus} />
          </div>
          {cat.gateStatus === 'Blocked' && (
            <p
              role="alert"
              className="rounded-[var(--radius-sm)] bg-danger-bg px-3 py-2 text-sm font-semibold text-danger-ink"
            >
              {cat.catName}’s vaccination is overdue. An administrator’s booking override reserves a
              unit but does not admit the cat — check-in will be refused until the vaccination is up
              to date.
            </p>
          )}
          <div className="flex flex-col gap-2">
            <label className="flex items-center gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={draft.identityConfirmed}
                onChange={(e) => patch(step, { identityConfirmed: e.target.checked })}
              />
              I have confirmed this is {cat.catName} (microchip or markings)
            </label>
            <FieldError id="ci-identity" message={err.identityConfirmed} />
            <label className="flex items-center gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={draft.vaccinationProofVerified}
                onChange={(e) => patch(step, { vaccinationProofVerified: e.target.checked })}
              />
              I have seen proof of the vaccinations
            </label>
            <FieldError id="ci-proof" message={err.vaccinationProofVerified} />
          </div>
          <div className="flex items-end gap-2">
            <FormFieldRow
              id={`ci-weight-${step}`}
              label="Weight"
              inputMode="decimal"
              required
              className="max-w-40"
              value={draft.weightKg}
              error={err.weightKg}
              onChange={(e) => patch(step, { weightKg: e.target.value })}
            />
            <span className="pb-2 text-sm font-semibold text-ink-muted">kg</span>
          </div>
          <TextAreaRow
            id={`ci-obs-${step}`}
            label="Arrival health observation"
            required
            value={draft.arrivalHealthObservation}
            error={err.arrivalHealthObservation}
            onChange={(e) => patch(step, { arrivalHealthObservation: e.target.value })}
          />
          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-semibold text-ink">Belongings</legend>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input
                type="checkbox"
                checked={draft.noBelongings}
                onChange={(e) => patch(step, { noBelongings: e.target.checked })}
              />
              No belongings
            </label>
            {!draft.noBelongings &&
              draft.belongings.map((b, bi) => (
                <div
                  key={bi}
                  className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3"
                >
                  <div className="grid gap-2 sm:grid-cols-[10rem_1fr_auto]">
                    <SelectRow
                      id={`ci-bkind-${step}-${bi}`}
                      label="Kind"
                      value={b.kind}
                      onChange={(e) =>
                        patch(step, {
                          belongings: draft.belongings.map((x, xi) =>
                            xi === bi ? { ...x, kind: e.target.value } : x,
                          ),
                        })
                      }
                    >
                      {Object.entries(BELONGING_KIND_LABEL).map(([k, v]) => (
                        <option key={k} value={k}>
                          {v}
                        </option>
                      ))}
                    </SelectRow>
                    <FormFieldRow
                      id={`ci-bdesc-${step}-${bi}`}
                      label="Description"
                      value={b.description}
                      onChange={(e) =>
                        patch(step, {
                          belongings: draft.belongings.map((x, xi) =>
                            xi === bi ? { ...x, description: e.target.value } : x,
                          ),
                        })
                      }
                    />
                    <div className="flex items-end">
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={`Remove belonging ${bi + 1}`}
                        onClick={() =>
                          patch(step, { belongings: draft.belongings.filter((_, xi) => xi !== bi) })
                        }
                      >
                        <Trash2 size={14} aria-hidden="true" /> Remove
                      </Button>
                    </div>
                  </div>
                  <FileUploader
                    id={`ci-bphoto-${step}-${bi}`}
                    label="Add photos of this item"
                    purpose="BoardingBelongings"
                    audience="staff"
                    hint="Photos only (JPEG, PNG or WebP). Some branches require at least one photo of the belongings."
                    onUploaded={(file) =>
                      patch(step, {
                        belongings: draft.belongings.map((x, xi) =>
                          xi === bi
                            ? {
                                ...x,
                                fileIds: [...x.fileIds, file.fileId],
                                fileNames: [...x.fileNames, file.originalFileName],
                              }
                            : x,
                        ),
                      })
                    }
                  />
                  {b.fileIds.length > 0 && (
                    <ul aria-label="Photos added" className="flex flex-col gap-1 text-sm">
                      {b.fileNames.map((name, fi) => (
                        <li key={b.fileIds[fi]} className="flex items-center justify-between gap-2">
                          <span>{name}</span>
                          <Button
                            variant="ghost"
                            size="sm"
                            aria-label={`Remove photo ${name}`}
                            onClick={() =>
                              patch(step, {
                                belongings: draft.belongings.map((x, xi) =>
                                  xi === bi
                                    ? {
                                        ...x,
                                        fileIds: x.fileIds.filter((_, i) => i !== fi),
                                        fileNames: x.fileNames.filter((_, i) => i !== fi),
                                      }
                                    : x,
                                ),
                              })
                            }
                          >
                            Remove
                          </Button>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              ))}
            {!draft.noBelongings && (
              <div>
                <Button
                  variant="secondary"
                  size="sm"
                  onClick={() =>
                    patch(step, {
                      belongings: [
                        ...draft.belongings,
                        {
                          kind: 'Carrier',
                          description: '',
                          fileIds: [],
                          fileNames: [],
                        },
                      ],
                    })
                  }
                >
                  <Plus size={14} aria-hidden="true" /> Add a belonging
                </Button>
              </div>
            )}
            <FieldError id="ci-bel" message={err.belongings} />
          </fieldset>
          <SignaturePad
            idPrefix={`ci-sign-${step}`}
            value={draft.signature}
            onChange={(signature) => patch(step, { signature })}
            error={err.signature}
          />
          <div className="flex gap-3">
            {step > 0 && (
              <Button variant="secondary" onClick={() => setStep(step - 1)}>
                Back
              </Button>
            )}
            <Button onClick={next}>{step === stay.cats.length - 1 ? 'Review' : 'Next cat'}</Button>
          </div>
        </div>
      )}

      {step === summaryStep && (
        <div className="flex flex-col gap-3">
          <ul aria-label="Check-in summary" className="flex flex-col gap-2 text-sm">
            {stay.cats.map((c, i) => {
              const d = drafts[i]!;
              return (
                <li key={c.catId} className="rounded-[var(--radius-sm)] border border-border p-3">
                  <strong>{c.catName}</strong> · {d.weightKg || '—'} kg ·{' '}
                  {d.noBelongings
                    ? 'no belongings'
                    : `${d.belongings.length} belonging${d.belongings.length === 1 ? '' : 's'}`}{' '}
                  · signed by {d.signature.signerName || '—'} (
                  {d.signature.method === 'Typed' ? 'typed name' : 'drawn'})
                  {Object.keys(errors[i] ?? {}).length > 0 && (
                    <span className="font-semibold text-danger-ink"> · needs attention</span>
                  )}
                </li>
              );
            })}
          </ul>
          <div className="flex gap-3">
            <Button variant="secondary" onClick={() => setStep(summaryStep - 1)}>
              Back
            </Button>
            <Button
              onClick={() => void submit()}
              disabled={checkIn.isPending}
              aria-busy={checkIn.isPending}
            >
              {checkIn.isPending ? 'Checking in…' : 'Check in'}
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}
