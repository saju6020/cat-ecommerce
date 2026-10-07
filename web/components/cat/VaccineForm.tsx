'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { submitWithServerErrors } from '@/lib/api/errors';

/**
 * `components/cat/VaccineForm.tsx` — the vaccine-catalogue register screen US-CAT-05 depends on
 * (HO-CAT-002 §3.2). Mirrors `RegisterVaccineCommandValidator` (HO-CAT-001 §11.3):
 * `validityMonths` in `1..120`; a duplicate `code` comes back as `VaccineCodeAlreadyExists`
 * mapped onto the `code` field.
 */
const VACCINE_FIELD_MAP = {
  Code: 'code',
  Name: 'name',
  IsCore: 'isCore',
  ValidityMonths: 'validityMonths',
  Description: 'description',
} as const;
const VACCINE_KNOWN_FIELDS = Object.values(VACCINE_FIELD_MAP);

const vaccineFormSchema = z.object({
  code: z.string().trim().min(1, 'Enter a code.').max(50),
  name: z.string().trim().min(1, 'Enter a name.').max(200),
  isCore: z.boolean(),
  // Plain z.number() + register(..., { valueAsNumber: true }) — see
  // components/cat/WeightEntryForm.tsx's comment on why not z.coerce.number().
  validityMonths: z
    .number({ error: 'Enter a validity period.' })
    .int()
    .min(1, 'Enter a validity between 1 and 120 months.')
    .max(120, 'Enter a validity between 1 and 120 months.'),
  description: z.string().max(500).optional(),
});

export type VaccineFormValues = z.infer<typeof vaccineFormSchema>;

export interface VaccineFormProps {
  onSubmit: (values: VaccineFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function VaccineForm({ onSubmit, isSubmitting }: VaccineFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<VaccineFormValues>({
    resolver: zodResolver(vaccineFormSchema),
    defaultValues: { code: '', name: '', isCore: false, validityMonths: 12, description: '' },
  });

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: VACCINE_FIELD_MAP,
          knownFields: VACCINE_KNOWN_FIELDS,
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
        <label htmlFor="code" className="text-sm font-semibold text-ink">
          Code{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="code"
          {...register('code')}
          required
          aria-invalid={!!errors.code}
          aria-describedby={errors.code ? 'code-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.code && (
          <p id="code-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.code.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="name" className="text-sm font-semibold text-ink">
          Name{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="name"
          {...register('name')}
          required
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? 'name-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.name && (
          <p id="name-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.name.message}
          </p>
        )}
      </div>

      <div className="flex items-center gap-2">
        <input id="isCore" type="checkbox" {...register('isCore')} className="h-4 w-4" />
        <label htmlFor="isCore" className="text-sm font-semibold text-ink">
          Core vaccine
        </label>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="validityMonths" className="text-sm font-semibold text-ink">
          Validity (months){' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="validityMonths"
          type="number"
          min={1}
          max={120}
          {...register('validityMonths', { valueAsNumber: true })}
          required
          aria-invalid={!!errors.validityMonths}
          aria-describedby={errors.validityMonths ? 'validityMonths-error' : undefined}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.validityMonths && (
          <p
            id="validityMonths-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.validityMonths.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="description" className="text-sm font-semibold text-ink">
          Description
        </label>
        <textarea
          id="description"
          rows={2}
          {...register('description')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Register vaccine'}
        </Button>
      </div>
    </form>
  );
}
