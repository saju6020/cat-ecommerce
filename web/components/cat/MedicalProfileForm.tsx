'use client';

import * as React from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { ALLERGY_SEVERITY_OPTIONS } from '@/lib/catEnumOptions';
import { submitWithServerErrors } from '@/lib/api/errors';

// Only the two top-level note fields are mappable — a per-row server error (unlikely, since the
// command replaces the collections wholesale rather than validating existing rows by index)
// falls back to the root/form-level message (HO-CAT-002 §11.8's "never swallowed").
const MEDICAL_PROFILE_FIELD_MAP = {
  DietaryNotes: 'dietaryNotes',
  BehaviouralNotes: 'behaviouralNotes',
} as const;
const MEDICAL_PROFILE_KNOWN_FIELDS = Object.values(MEDICAL_PROFILE_FIELD_MAP);

/**
 * `components/cat/MedicalProfileForm.tsx` — US-CAT-03 (HO-CAT-002 §11.7). Repeatable rows for
 * allergies, chronic conditions and medications; submits the whole set, because
 * `UpdateCatMedicalProfileCommand` (HO-CAT-001 §11.2 C5) replaces all three collections
 * wholesale rather than diffing them.
 *
 * Removing a medication row confirms first (UI-3's list of destructive actions names this one
 * specifically) — the other two row types don't carry the same clinical weight of "this cat is
 * currently on this drug" and aren't named there, so they remove immediately.
 */
const allergySchema = z.object({
  allergen: z.string().trim().min(1, 'Enter the allergen.').max(200),
  reaction: z.string().trim().min(1, 'Enter the reaction.').max(500),
  severity: z.enum(ALLERGY_SEVERITY_OPTIONS as [string, ...string[]]),
  recordedOn: z.string().min(1, 'Enter the date this was recorded.'),
});

const chronicConditionSchema = z.object({
  condition: z.string().trim().min(1, 'Enter the condition.').max(200),
  diagnosedOn: z.string().optional(),
  notes: z.string().max(1000).optional(),
});

const medicationSchema = z.object({
  medicationName: z.string().trim().min(1, 'Enter the medication name.').max(200),
  dosage: z.string().trim().min(1, 'Enter the dosage.').max(100),
  frequency: z.string().trim().min(1, 'Enter the frequency.').max(100),
  startedOn: z.string().min(1, 'Enter the start date.'),
  endsOn: z.string().optional(),
});

const medicalProfileFormSchema = z.object({
  allergies: z.array(allergySchema),
  chronicConditions: z.array(chronicConditionSchema),
  medications: z.array(medicationSchema),
  dietaryNotes: z.string().max(1000, 'Dietary notes must be 1000 characters or fewer.').optional(),
  behaviouralNotes: z
    .string()
    .max(1000, 'Behavioural notes must be 1000 characters or fewer.')
    .optional(),
});

export type MedicalProfileFormValues = z.infer<typeof medicalProfileFormSchema>;

export interface MedicalProfileFormProps {
  defaultValues?: Partial<MedicalProfileFormValues>;
  onSubmit: (values: MedicalProfileFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function MedicalProfileForm({
  defaultValues,
  onSubmit,
  isSubmitting,
}: MedicalProfileFormProps) {
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<MedicalProfileFormValues>({
    resolver: zodResolver(medicalProfileFormSchema),
    defaultValues: {
      allergies: [],
      chronicConditions: [],
      medications: [],
      dietaryNotes: '',
      behaviouralNotes: '',
      ...defaultValues,
    },
  });

  const allergies = useFieldArray({ control, name: 'allergies' });
  const chronicConditions = useFieldArray({ control, name: 'chronicConditions' });
  const medications = useFieldArray({ control, name: 'medications' });

  const [pendingMedicationRemoval, setPendingMedicationRemoval] = React.useState<number | null>(
    null,
  );

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: MEDICAL_PROFILE_FIELD_MAP,
          knownFields: MEDICAL_PROFILE_KNOWN_FIELDS,
        }),
      )}
      noValidate
      className="flex flex-col gap-8"
    >
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <section aria-labelledby="allergies-heading" className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <h2 id="allergies-heading" className="text-sm font-bold text-ink">
            Allergies
          </h2>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() =>
              allergies.append({ allergen: '', reaction: '', severity: 'Mild', recordedOn: '' })
            }
          >
            Add allergy
          </Button>
        </div>
        {allergies.fields.length === 0 && <p className="text-sm text-ink-muted">None recorded.</p>}
        {allergies.fields.map((field, index) => (
          <fieldset
            key={field.id}
            className="grid gap-3 rounded-[var(--radius-md)] border border-border p-4 sm:grid-cols-2"
          >
            <legend className="sr-only">Allergy {index + 1}</legend>
            <FormFieldRow
              id={`allergies.${index}.allergen`}
              label="Allergen"
              error={errors.allergies?.[index]?.allergen?.message}
              {...register(`allergies.${index}.allergen` as const)}
            />
            <FormFieldRow
              id={`allergies.${index}.reaction`}
              label="Reaction"
              error={errors.allergies?.[index]?.reaction?.message}
              {...register(`allergies.${index}.reaction` as const)}
            />
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor={`allergies.${index}.severity`}
                className="text-sm font-semibold text-ink"
              >
                Severity
              </label>
              <select
                id={`allergies.${index}.severity`}
                {...register(`allergies.${index}.severity` as const)}
                className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
              >
                {ALLERGY_SEVERITY_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <FormFieldRow
              id={`allergies.${index}.recordedOn`}
              label="Recorded on"
              type="date"
              error={errors.allergies?.[index]?.recordedOn?.message}
              {...register(`allergies.${index}.recordedOn` as const)}
            />
            <div className="sm:col-span-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => allergies.remove(index)}
              >
                <Trash2 size={14} aria-hidden="true" /> Remove allergy
              </Button>
            </div>
          </fieldset>
        ))}
      </section>

      <section aria-labelledby="conditions-heading" className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <h2 id="conditions-heading" className="text-sm font-bold text-ink">
            Chronic conditions
          </h2>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => chronicConditions.append({ condition: '', diagnosedOn: '', notes: '' })}
          >
            Add condition
          </Button>
        </div>
        {chronicConditions.fields.length === 0 && (
          <p className="text-sm text-ink-muted">None recorded.</p>
        )}
        {chronicConditions.fields.map((field, index) => (
          <fieldset
            key={field.id}
            className="grid gap-3 rounded-[var(--radius-md)] border border-border p-4 sm:grid-cols-2"
          >
            <legend className="sr-only">Chronic condition {index + 1}</legend>
            <FormFieldRow
              id={`chronicConditions.${index}.condition`}
              label="Condition"
              className="sm:col-span-2"
              error={errors.chronicConditions?.[index]?.condition?.message}
              {...register(`chronicConditions.${index}.condition` as const)}
            />
            <FormFieldRow
              id={`chronicConditions.${index}.diagnosedOn`}
              label="Diagnosed on"
              type="date"
              {...register(`chronicConditions.${index}.diagnosedOn` as const)}
            />
            <FormFieldRow
              id={`chronicConditions.${index}.notes`}
              label="Notes"
              {...register(`chronicConditions.${index}.notes` as const)}
            />
            <div className="sm:col-span-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => chronicConditions.remove(index)}
              >
                <Trash2 size={14} aria-hidden="true" /> Remove condition
              </Button>
            </div>
          </fieldset>
        ))}
      </section>

      <section aria-labelledby="medications-heading" className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <h2 id="medications-heading" className="text-sm font-bold text-ink">
            Medications
          </h2>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() =>
              medications.append({
                medicationName: '',
                dosage: '',
                frequency: '',
                startedOn: '',
                endsOn: '',
              })
            }
          >
            Add medication
          </Button>
        </div>
        {medications.fields.length === 0 && (
          <p className="text-sm text-ink-muted">None recorded.</p>
        )}
        {medications.fields.map((field, index) => (
          <fieldset
            key={field.id}
            className="grid gap-3 rounded-[var(--radius-md)] border border-border p-4 sm:grid-cols-2"
          >
            <legend className="sr-only">Medication {index + 1}</legend>
            <FormFieldRow
              id={`medications.${index}.medicationName`}
              label="Medication"
              className="sm:col-span-2"
              error={errors.medications?.[index]?.medicationName?.message}
              {...register(`medications.${index}.medicationName` as const)}
            />
            <FormFieldRow
              id={`medications.${index}.dosage`}
              label="Dosage"
              placeholder="e.g. 2.5 mg"
              error={errors.medications?.[index]?.dosage?.message}
              {...register(`medications.${index}.dosage` as const)}
            />
            <FormFieldRow
              id={`medications.${index}.frequency`}
              label="Frequency"
              placeholder="e.g. Once daily"
              error={errors.medications?.[index]?.frequency?.message}
              {...register(`medications.${index}.frequency` as const)}
            />
            <FormFieldRow
              id={`medications.${index}.startedOn`}
              label="Started on"
              type="date"
              error={errors.medications?.[index]?.startedOn?.message}
              {...register(`medications.${index}.startedOn` as const)}
            />
            <FormFieldRow
              id={`medications.${index}.endsOn`}
              label="Ends on"
              type="date"
              {...register(`medications.${index}.endsOn` as const)}
            />
            <div className="sm:col-span-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setPendingMedicationRemoval(index)}
              >
                <Trash2 size={14} aria-hidden="true" /> Remove medication
              </Button>
            </div>
          </fieldset>
        ))}
      </section>

      <section className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="dietaryNotes" className="text-sm font-semibold text-ink">
            Dietary notes
          </label>
          <textarea
            id="dietaryNotes"
            rows={3}
            {...register('dietaryNotes')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="behaviouralNotes" className="text-sm font-semibold text-ink">
            Behavioural notes
          </label>
          <textarea
            id="behaviouralNotes"
            rows={3}
            {...register('behaviouralNotes')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
      </section>

      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Save medical profile'}
        </Button>
      </div>

      <ConfirmDialog
        open={pendingMedicationRemoval !== null}
        onOpenChange={(open) => !open && setPendingMedicationRemoval(null)}
        title="Remove this medication?"
        consequence="This removes the medication from the cat's medical profile once you save. It won't be visible on the profile header or medical history until re-added."
        variant="destructive"
        confirmLabel="Remove medication"
        onConfirm={() => {
          if (pendingMedicationRemoval !== null) medications.remove(pendingMedicationRemoval);
          setPendingMedicationRemoval(null);
        }}
      />
    </form>
  );
}
