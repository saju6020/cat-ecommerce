'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { NEUTER_STATUS_OPTIONS } from '@/lib/catEnumOptions';
import { submitWithServerErrors } from '@/lib/api/errors';

const NEUTER_STATUS_FIELD_MAP = { NeuterStatus: 'neuterStatus', NeuteredOn: 'neuteredOn' } as const;
const NEUTER_STATUS_KNOWN_FIELDS = Object.values(NEUTER_STATUS_FIELD_MAP);

/**
 * `components/cat/NeuterStatusForm.tsx` — US-CAT-02 (HO-CAT-002 §11.7). "Date required and
 * shown only when 'Neutered' is selected" — mirrors `RecordCatNeuterStatusCommandValidator`
 * (HO-CAT-001 §11.3): `NeuteredOn` required when `NeuterStatus == Neutered`. Component test U20.
 */
const neuterStatusFormSchema = z
  .object({
    neuterStatus: z.enum(NEUTER_STATUS_OPTIONS as [string, ...string[]]),
    neuteredOn: z.string().optional(),
  })
  .refine((data) => data.neuterStatus !== 'Neutered' || !!data.neuteredOn, {
    message: 'Enter the date this cat was neutered.',
    path: ['neuteredOn'],
  });

export type NeuterStatusFormValues = z.infer<typeof neuterStatusFormSchema>;

export interface NeuterStatusFormProps {
  defaultValues?: Partial<NeuterStatusFormValues>;
  onSubmit: (values: NeuterStatusFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function NeuterStatusForm({ defaultValues, onSubmit, isSubmitting }: NeuterStatusFormProps) {
  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors },
  } = useForm<NeuterStatusFormValues>({
    resolver: zodResolver(neuterStatusFormSchema),
    defaultValues: { neuterStatus: 'Unknown', neuteredOn: '', ...defaultValues },
  });

  const status = watch('neuterStatus');

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: NEUTER_STATUS_FIELD_MAP,
          knownFields: NEUTER_STATUS_KNOWN_FIELDS,
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
        <label htmlFor="neuterStatus" className="text-sm font-semibold text-ink">
          Neuter status
        </label>
        <select
          id="neuterStatus"
          {...register('neuterStatus')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          {NEUTER_STATUS_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>

      {status === 'Neutered' && (
        <div className="flex flex-col gap-1.5">
          <label htmlFor="neuteredOn" className="text-sm font-semibold text-ink">
            Date neutered{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </label>
          <input
            id="neuteredOn"
            type="date"
            {...register('neuteredOn')}
            required
            aria-invalid={!!errors.neuteredOn}
            aria-describedby={errors.neuteredOn ? 'neuteredOn-error' : undefined}
            className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          {errors.neuteredOn && (
            <p id="neuteredOn-error" role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.neuteredOn.message}
            </p>
          )}
        </div>
      )}

      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Save neuter status'}
        </Button>
      </div>
    </form>
  );
}
