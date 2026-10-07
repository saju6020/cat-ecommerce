'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { submitWithServerErrors } from '@/lib/api/errors';
import { todayAsDateOnly } from '@/lib/dateOnly';
import type { VaccineListItem } from '@/lib/api/vaccines';

/**
 * `components/cat/VaccinationForm.tsx` — US-CAT-05 (HO-CAT-002 §11.7). Vaccine picked from the
 * catalogue (`vaccines` prop — the page owns the `useVaccines` query, §8.2 rule 4); batch
 * reference, batch expiry, date given, administering vet. `ExpiredVaccineBatch` (HO-CAT-001
 * §8.2's `BatchExpiresOn >= AdministeredOn` invariant) surfaces as a field error on
 * `batchExpiresOn` — component/e2e evidence U-none/E7.
 */
const VACCINATION_FIELD_MAP = {
  VaccineId: 'vaccineId',
  BatchReference: 'batchReference',
  BatchExpiresOn: 'batchExpiresOn',
  AdministeredOn: 'administeredOn',
  AdministeredByVetId: 'administeredByVetId',
  Notes: 'notes',
} as const;
const VACCINATION_KNOWN_FIELDS = Object.values(VACCINATION_FIELD_MAP);

const vaccinationFormSchema = z.object({
  vaccineId: z.string().min(1, 'Choose a vaccine.'),
  batchReference: z.string().trim().min(1, 'Enter the batch reference.').max(100),
  batchExpiresOn: z.string().min(1, 'Enter the batch expiry date.'),
  administeredOn: z.string().min(1, 'Enter the date given.'),
  administeredByVetId: z.string().trim().min(1, 'Enter the administering vet.'),
  notes: z.string().max(500).optional(),
});

export type VaccinationFormValues = z.infer<typeof vaccinationFormSchema>;

export interface VaccinationFormProps {
  vaccines: VaccineListItem[];
  onSubmit: (values: VaccinationFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function VaccinationForm({ vaccines, onSubmit, isSubmitting }: VaccinationFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<VaccinationFormValues>({
    resolver: zodResolver(vaccinationFormSchema),
    defaultValues: {
      vaccineId: vaccines[0]?.id ?? '',
      batchReference: '',
      batchExpiresOn: '',
      administeredOn: todayAsDateOnly(),
      administeredByVetId: '',
      notes: '',
    },
  });

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: VACCINATION_FIELD_MAP,
          knownFields: VACCINATION_KNOWN_FIELDS,
        }),
      )}
      noValidate
      className="flex max-w-md flex-col gap-4"
    >
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="vaccineId" className="text-sm font-semibold text-ink">
          Vaccine{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <select
          id="vaccineId"
          {...register('vaccineId')}
          required
          aria-invalid={!!errors.vaccineId}
          aria-describedby={errors.vaccineId ? 'vaccineId-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          {vaccines.length === 0 && <option value="">No vaccines in the catalogue yet</option>}
          {vaccines.map((vaccine) => (
            <option key={vaccine.id} value={vaccine.id}>
              {vaccine.name} ({vaccine.code}){vaccine.isCore ? ' — core' : ''}
            </option>
          ))}
        </select>
        {errors.vaccineId && (
          <p id="vaccineId-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.vaccineId.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="batchReference" className="text-sm font-semibold text-ink">
          Batch reference{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="batchReference"
          {...register('batchReference')}
          required
          aria-invalid={!!errors.batchReference}
          aria-describedby={errors.batchReference ? 'batchReference-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.batchReference && (
          <p
            id="batchReference-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.batchReference.message}
          </p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="administeredOn" className="text-sm font-semibold text-ink">
            Date given{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </label>
          <input
            id="administeredOn"
            type="date"
            {...register('administeredOn')}
            required
            aria-invalid={!!errors.administeredOn}
            aria-describedby={errors.administeredOn ? 'administeredOn-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.administeredOn && (
            <p
              id="administeredOn-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {errors.administeredOn.message}
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="batchExpiresOn" className="text-sm font-semibold text-ink">
            Batch expiry{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </label>
          <input
            id="batchExpiresOn"
            type="date"
            {...register('batchExpiresOn')}
            required
            aria-invalid={!!errors.batchExpiresOn}
            aria-describedby={errors.batchExpiresOn ? 'batchExpiresOn-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.batchExpiresOn && (
            <p
              id="batchExpiresOn-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {errors.batchExpiresOn.message}
            </p>
          )}
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="administeredByVetId" className="text-sm font-semibold text-ink">
          Administering vet{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="administeredByVetId"
          {...register('administeredByVetId')}
          required
          aria-invalid={!!errors.administeredByVetId}
          aria-describedby={errors.administeredByVetId ? 'administeredByVetId-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.administeredByVetId && (
          <p
            id="administeredByVetId-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.administeredByVetId.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="vaccination-notes" className="text-sm font-semibold text-ink">
          Notes
        </label>
        <textarea
          id="vaccination-notes"
          rows={2}
          {...register('notes')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Record vaccination'}
        </Button>
      </div>
    </form>
  );
}
