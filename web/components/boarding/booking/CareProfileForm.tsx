'use client';

import { useEffect, useRef, useState } from 'react';
import { FormProvider, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { FormErrorAlert, SelectRow, TextAreaRow } from '@/components/boarding/shared/FormBits';
import { LITTER_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { submitWithServerErrors, BOARDING_VIOLATION_MESSAGES } from '@/lib/api/errors';
import type { CareProfile } from '@/lib/api/types';
import {
  LITTER_TYPES,
  careProfileSchema,
  formToCareProfile,
  type CareProfileFormValues,
} from '@/lib/validation/boarding';
import { FeedingRows, MedicationRows } from './CareProfileRows';

const FIELD_MAP = {
  Feedings: 'feedings',
  Medications: 'medications',
  LitterType: 'litterType',
  LitterNote: 'litterNote',
  VetVisitAuthorised: 'vetVisitAuthorised',
  VetSpendingLimit: 'vetSpendingLimit',
  EmergencyContact: 'emergencyName',
  Notes: 'notes',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/**
 * `components/boarding/booking/CareProfileForm.tsx` — ⭐ HO-BRD-002 §11.1 step 5, US-BRD-05/07: one
 * cat's care profile — feeding schedule, medication, litter, **vet-visit authorisation** (explicit
 * yes/no, with what it means), the **veterinary spending limit** (per cat), emergency contact and
 * notes. An explicit **Save** per cat; values survive a validation or server error (NFR-USA-5). The
 * limit is entered as text and sent as text — never computed on (ADR-FE-105). The owning screen
 * mounts one `UnsavedChangesGuard` across all the cats' forms via `onDirtyChange`.
 */
export interface CareProfileFormProps {
  catId: string;
  catName: string;
  initial: CareProfileFormValues;
  /** e.g. "from Mittens’s profile" — says where the prefilled medication and notes came from. */
  prefilledNote?: string | null;
  currencyCode: string;
  onSave: (
    catId: string,
    profile: CareProfile,
    extra?: { vetAuthorityChangeReason?: string },
  ) => Promise<unknown>;
  /** Staff on a stay past Enquiry: changing the authorisation or limit needs a reason (HO-BRD-001 fc6ef889). */
  requireAuthorityReason?: boolean;
  onDirtyChange?: (catId: string, dirty: boolean) => void;
  readOnly?: boolean;
}

export function CareProfileForm({
  catId,
  catName,
  initial,
  prefilledNote,
  currencyCode,
  onSave,
  requireAuthorityReason,
  onDirtyChange,
  readOnly,
}: CareProfileFormProps) {
  const methods = useForm<CareProfileFormValues>({
    resolver: zodResolver(careProfileSchema),
    defaultValues: initial,
  });
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = methods;
  const idPrefix = `cp-${catId}`;
  const [saved, setSaved] = useState(false);
  const [reason, setReason] = useState('');
  const [reasonError, setReasonError] = useState<string | null>(null);
  const baseline = useRef({ auth: initial.vetVisitAuthorised, limit: initial.vetSpendingLimit });
  const watchedAuth = methods.watch('vetVisitAuthorised');
  const watchedLimit = methods.watch('vetSpendingLimit');
  const authorityChanged =
    !!requireAuthorityReason &&
    (watchedAuth !== baseline.current.auth || watchedLimit !== baseline.current.limit);

  useEffect(() => {
    onDirtyChange?.(catId, isDirty);
    if (isDirty) setSaved(false);
  }, [catId, isDirty, onDirtyChange]);

  async function submit(values: CareProfileFormValues) {
    const changed =
      !!requireAuthorityReason &&
      (values.vetVisitAuthorised !== baseline.current.auth ||
        values.vetSpendingLimit !== baseline.current.limit);
    if (changed && !reason.trim()) {
      setReasonError('Give a reason for changing the vet authorisation or the limit.');
      return;
    }
    setReasonError(null);
    const result = await submitWithServerErrors(
      // `submitWithServerErrors` returns undefined on failure, so a void save resolves to `true`.
      async () => {
        await onSave(
          catId,
          formToCareProfile(values, currencyCode),
          changed ? { vetAuthorityChangeReason: reason.trim() } : undefined,
        );
        return true;
      },
      setError,
      { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS, codeMessages: BOARDING_VIOLATION_MESSAGES },
    );
    if (result === true) {
      reset(values);
      baseline.current = { auth: values.vetVisitAuthorised, limit: values.vetSpendingLimit };
      setReason('');
      setSaved(true);
    }
  }

  return (
    <FormProvider {...methods}>
      <form
        onSubmit={handleSubmit(submit)}
        noValidate
        aria-label={`Care profile for ${catName}`}
        className="flex flex-col gap-6"
      >
        {prefilledNote && (
          <p className="rounded-[var(--radius-sm)] bg-accent-soft px-3 py-2 text-sm text-accent-ink">
            {prefilledNote} — edit anything that is different for this stay.
          </p>
        )}
        <FormErrorAlert message={errors.root?.message} />
        {saved && !isDirty && (
          <p role="status" className="text-sm font-semibold text-accent-ink">
            {catName}’s care profile is saved.
          </p>
        )}

        <fieldset disabled={readOnly} className="flex flex-col gap-6">
          <FeedingRows idPrefix={idPrefix} />
          <MedicationRows idPrefix={idPrefix} />

          <fieldset className="grid gap-3 sm:grid-cols-2">
            <legend className="text-sm font-semibold text-ink">Litter</legend>
            <SelectRow
              id={`${idPrefix}-litter`}
              label="Litter type"
              error={errors.litterType?.message}
              {...register('litterType')}
            >
              {LITTER_TYPES.map((t) => (
                <option key={t} value={t}>
                  {LITTER_TYPE_LABEL[t]}
                </option>
              ))}
            </SelectRow>
            <FormFieldRow
              id={`${idPrefix}-litter-note`}
              label="Litter note (optional)"
              error={errors.litterNote?.message}
              {...register('litterNote')}
            />
          </fieldset>

          <fieldset className="flex flex-col gap-2" aria-describedby={`${idPrefix}-vet-help`}>
            <legend className="text-sm font-semibold text-ink">
              May the clinic examine and treat {catName} if needed?
            </legend>
            <p id={`${idPrefix}-vet-help`} className="text-xs text-ink-muted">
              “Yes” lets the clinic’s vet examine and treat {catName} during the stay, up to your
              spending limit. “No” means we will always ask you first.
            </p>
            <div className="flex gap-5">
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="radio" value="yes" {...register('vetVisitAuthorised')} /> Yes, treat if
                needed
              </label>
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="radio" value="no" {...register('vetVisitAuthorised')} /> No, ask me
                first
              </label>
            </div>
            {errors.vetVisitAuthorised && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.vetVisitAuthorised.message}
              </p>
            )}
            <FormFieldRow
              id={`${idPrefix}-limit`}
              label={`Veterinary spending limit (${currencyCode})`}
              inputMode="decimal"
              placeholder="e.g. 2000"
              required
              className="max-w-xs"
              error={errors.vetSpendingLimit?.message}
              {...register('vetSpendingLimit')}
            />
            {authorityChanged && (
              <TextAreaRow
                id={`${idPrefix}-authority-reason`}
                label="Reason for changing the vet authorisation or the limit"
                hint="Recorded in the audit trail."
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                error={reasonError ?? undefined}
              />
            )}
          </fieldset>

          <fieldset className="grid gap-3 sm:grid-cols-3">
            <legend className="text-sm font-semibold text-ink">Emergency contact</legend>
            <FormFieldRow
              id={`${idPrefix}-em-name`}
              label="Name"
              required
              error={errors.emergencyName?.message}
              {...register('emergencyName')}
            />
            <FormFieldRow
              id={`${idPrefix}-em-phone`}
              label="Phone"
              type="tel"
              required
              error={errors.emergencyPhone?.message}
              {...register('emergencyPhone')}
            />
            <FormFieldRow
              id={`${idPrefix}-em-rel`}
              label="Relationship (optional)"
              error={errors.emergencyRelationship?.message}
              {...register('emergencyRelationship')}
            />
          </fieldset>

          <TextAreaRow
            id={`${idPrefix}-notes`}
            label="Notes (optional)"
            hint="Behaviour, routine, anything we should know."
            error={errors.notes?.message}
            {...register('notes')}
          />
        </fieldset>

        {!readOnly && (
          <div>
            <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
              <Save size={16} aria-hidden="true" />
              {isSubmitting ? 'Saving…' : `Save ${catName}’s care profile`}
            </Button>
          </div>
        )}
      </form>
    </FormProvider>
  );
}
