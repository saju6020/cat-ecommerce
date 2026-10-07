'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { submitWithServerErrors } from '@/lib/api/errors';
import { todayAsDateOnly } from '@/lib/dateOnly';
import { PARASITE_TREATMENT_TYPE_OPTIONS } from '@/lib/catEnumOptions';

/**
 * `components/cat/ParasiteTreatmentForm.tsx` — US-CAT-06 (HO-CAT-002 §11.7). Type, product,
 * date, interval. The server-computed next-due date is shown by the caller (the
 * parasite-control page) after saving, straight from the `201` response's `nextDueOn`
 * (HO-CAT-001 C10, amended 2026-09-25) — this form never computes or displays it itself.
 */
const PARASITE_TREATMENT_FIELD_MAP = {
  TreatmentType: 'treatmentType',
  ProductName: 'productName',
  BatchReference: 'batchReference',
  AdministeredOn: 'administeredOn',
  AdministeredByStaffId: 'administeredByStaffId',
  IntervalDays: 'intervalDays',
  Notes: 'notes',
} as const;
const PARASITE_TREATMENT_KNOWN_FIELDS = Object.values(PARASITE_TREATMENT_FIELD_MAP);

const parasiteTreatmentFormSchema = z.object({
  treatmentType: z.enum(PARASITE_TREATMENT_TYPE_OPTIONS as [string, ...string[]]),
  productName: z.string().trim().min(1, 'Enter the product name.').max(200),
  batchReference: z.string().max(100).optional(),
  administeredOn: z.string().min(1, 'Enter the date given.'),
  administeredByStaffId: z.string().trim().min(1, 'Enter the administering staff member.'),
  // Plain z.number() + register(..., { valueAsNumber: true }) — see WeightEntryForm.tsx's
  // comment on why not z.coerce.number().
  intervalDays: z
    .number({ error: 'Enter an interval.' })
    .int()
    .min(1, 'Enter an interval between 1 and 730 days.')
    .max(730, 'Enter an interval between 1 and 730 days.'),
  notes: z.string().max(500).optional(),
});

export type ParasiteTreatmentFormValues = z.infer<typeof parasiteTreatmentFormSchema>;

export interface ParasiteTreatmentFormProps {
  onSubmit: (values: ParasiteTreatmentFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function ParasiteTreatmentForm({ onSubmit, isSubmitting }: ParasiteTreatmentFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ParasiteTreatmentFormValues>({
    resolver: zodResolver(parasiteTreatmentFormSchema),
    defaultValues: {
      treatmentType: 'Combined',
      productName: '',
      batchReference: '',
      administeredOn: todayAsDateOnly(),
      administeredByStaffId: '',
      intervalDays: 90,
      notes: '',
    },
  });

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: PARASITE_TREATMENT_FIELD_MAP,
          knownFields: PARASITE_TREATMENT_KNOWN_FIELDS,
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
        <label htmlFor="treatmentType" className="text-sm font-semibold text-ink">
          Treatment type
        </label>
        <select
          id="treatmentType"
          {...register('treatmentType')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          {PARASITE_TREATMENT_TYPE_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="productName" className="text-sm font-semibold text-ink">
          Product{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="productName"
          {...register('productName')}
          required
          aria-invalid={!!errors.productName}
          aria-describedby={errors.productName ? 'productName-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.productName && (
          <p id="productName-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.productName.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="batchReference" className="text-sm font-semibold text-ink">
          Batch reference
        </label>
        <input
          id="batchReference"
          {...register('batchReference')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
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
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="intervalDays" className="text-sm font-semibold text-ink">
            Interval (days){' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </label>
          <input
            id="intervalDays"
            type="number"
            min={1}
            max={730}
            {...register('intervalDays', { valueAsNumber: true })}
            required
            aria-invalid={!!errors.intervalDays}
            aria-describedby={errors.intervalDays ? 'intervalDays-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.intervalDays && (
            <p
              id="intervalDays-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {errors.intervalDays.message}
            </p>
          )}
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="administeredByStaffId" className="text-sm font-semibold text-ink">
          Administered by{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="administeredByStaffId"
          {...register('administeredByStaffId')}
          required
          aria-invalid={!!errors.administeredByStaffId}
          aria-describedby={
            errors.administeredByStaffId ? 'administeredByStaffId-error' : undefined
          }
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.administeredByStaffId && (
          <p
            id="administeredByStaffId-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.administeredByStaffId.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="parasite-notes" className="text-sm font-semibold text-ink">
          Notes
        </label>
        <textarea
          id="parasite-notes"
          rows={2}
          {...register('notes')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Record treatment'}
        </Button>
      </div>
    </form>
  );
}
