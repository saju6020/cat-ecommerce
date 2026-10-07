'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { FieldWithUnit } from '@/components/common/FieldWithUnit';
import { todayAsDateOnly } from '@/lib/dateOnly';
import { submitWithServerErrors } from '@/lib/api/errors';

const WEIGHT_ENTRY_FIELD_MAP = {
  Kilograms: 'kilograms',
  RecordedOn: 'recordedOn',
  Notes: 'notes',
} as const;
const WEIGHT_ENTRY_KNOWN_FIELDS = Object.values(WEIGHT_ENTRY_FIELD_MAP);

/**
 * `components/cat/WeightEntryForm.tsx` — US-CAT-02 (HO-CAT-002 §11.7). Uses `FieldWithUnit`
 * (UI-8 — unit always visible, never a bare number). Client-side range `0.10–30.00` kg mirrors
 * `RecordCatWeightCommandValidator` (HO-CAT-001 §11.3). Component test U21.
 */
const weightEntryFormSchema = z.object({
  // Plain z.number(), not z.coerce.number() — coercion gives the schema a string/unknown input
  // type distinct from its number output type, which zodResolver's generics don't reconcile
  // cleanly with react-hook-form's single TFieldValues. `register(..., { valueAsNumber: true })`
  // below does the string->number conversion instead, so the form's values are `number` both in
  // and out.
  kilograms: z
    .number({ error: 'Enter a weight.' })
    .min(0.1, 'Enter a weight between 0.10 and 30.00 kg.')
    .max(30, 'Enter a weight between 0.10 and 30.00 kg.'),
  recordedOn: z.string().min(1, 'Enter the date this weight was recorded.'),
  notes: z.string().max(500, 'Notes must be 500 characters or fewer.').optional(),
});

export type WeightEntryFormValues = z.infer<typeof weightEntryFormSchema>;

export interface WeightEntryFormProps {
  onSubmit: (values: WeightEntryFormValues) => Promise<unknown>;
  isSubmitting?: boolean;
}

export function WeightEntryForm({ onSubmit, isSubmitting }: WeightEntryFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<WeightEntryFormValues>({
    resolver: zodResolver(weightEntryFormSchema),
    defaultValues: { recordedOn: todayAsDateOnly(), notes: '' },
  });

  return (
    <form
      onSubmit={handleSubmit((values) =>
        submitWithServerErrors(() => onSubmit(values), setError, {
          fieldMap: WEIGHT_ENTRY_FIELD_MAP,
          knownFields: WEIGHT_ENTRY_KNOWN_FIELDS,
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

      <FieldWithUnit
        id="kilograms"
        label="Weight"
        unit="kg"
        step="0.01"
        min={0.1}
        max={30}
        required
        error={errors.kilograms?.message}
        {...register('kilograms', { valueAsNumber: true })}
      />

      <div className="flex flex-col gap-1.5">
        <label htmlFor="recordedOn" className="text-sm font-semibold text-ink">
          Date recorded{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="recordedOn"
          type="date"
          {...register('recordedOn')}
          required
          aria-invalid={!!errors.recordedOn}
          aria-describedby={errors.recordedOn ? 'recordedOn-error' : undefined}
          className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
        {errors.recordedOn && (
          <p id="recordedOn-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.recordedOn.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="notes" className="text-sm font-semibold text-ink">
          Notes
        </label>
        <textarea
          id="notes"
          rows={3}
          {...register('notes')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
      </div>

      <div>
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Record weight'}
        </Button>
      </div>
    </form>
  );
}
