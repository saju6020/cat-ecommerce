'use client';

import type { UseFormReturn } from 'react-hook-form';
import { CheckboxField, TextField } from '@/components/inventory/fields';
import type { ComposerValues } from './composerModel';

/**
 * `RepeatFields` — HO-PHM-002 §11.2. A *Repeatable* switch: on, the vet gives the number of repeats
 * (1 to 12, after the first supply) and the date they stop (required); off, an optional earlier
 * "dispense by" date. The server applies the clinic's default window when the date is left empty.
 */
export function RepeatFields({ form }: { form: UseFormReturn<ComposerValues> }) {
  const {
    register,
    watch,
    formState: { errors },
  } = form;
  const repeatable = watch('isRepeatable');
  return (
    <fieldset className="flex flex-col gap-3 border-t border-border pt-4">
      <legend className="sr-only">Repeats and validity</legend>
      <CheckboxField
        id="rx-repeatable"
        label="Repeatable"
        hint="The owner can ask for further supplies without a new consultation, until the date below."
        {...register('isRepeatable')}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        {repeatable && (
          <TextField
            id="rx-repeats"
            label="Repeats"
            required
            inputMode="numeric"
            hint="1 to 12, after the first supply."
            error={errors.repeatsAuthorised?.message}
            {...register('repeatsAuthorised')}
          />
        )}
        <TextField
          id="rx-valid-until"
          label={repeatable ? 'Valid until' : 'Dispense by (optional)'}
          type="date"
          required={repeatable}
          hint={
            repeatable
              ? 'Required. The date the repeats stop.'
              : 'Leave empty to use the clinic’s default window.'
          }
          error={errors.validUntil?.message}
          {...register('validUntil')}
        />
      </div>
    </fieldset>
  );
}
