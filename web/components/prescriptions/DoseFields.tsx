'use client';

import { Controller, type UseFormReturn } from 'react-hook-form';
import { QuantityInput } from '@/components/common/Quantity';
import { SelectField, TextAreaField } from '@/components/inventory/fields';
import type { FormularyMedicineDetail } from '@/lib/api/formulary';
import { FREQUENCY_OPTIONS, routeLabel } from '@/lib/format/pharmacy';
import { doseUnitsForForm } from '@/lib/format/units';
import type { ComposerValues } from './composerModel';

/**
 * `DoseFields` — HO-PHM-002 §11.2, ADR-FE-061. The dose is a number with a **unit select that starts
 * empty** (the vet chooses; the choices are the units the medicine's form allows), the route is limited
 * to the routes the medicine allows, the frequency is worded, the duration is in days, and the
 * quantity per supply is fixed to the medicine's dispensing unit. Every number has its unit beside it.
 */
export function DoseFields({
  form,
  medicine,
}: {
  form: UseFormReturn<ComposerValues>;
  medicine: FormularyMedicineDetail | undefined;
}) {
  const {
    control,
    register,
    watch,
    formState: { errors },
  } = form;
  const instructions = watch('instructions') ?? '';
  const disabled = !medicine;

  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-4 sm:grid-cols-3">
        <Controller
          control={control}
          name="doseAmount"
          render={({ field }) => (
            <QuantityInput
              id="rx-dose"
              label="Dose"
              required
              amount={field.value}
              unit={watch('doseUnit')}
              units={medicine ? doseUnitsForForm(medicine.form) : []}
              disabled={disabled}
              onChange={(next) => {
                field.onChange(next.amount);
                form.setValue('doseUnit', next.unit, {
                  shouldDirty: true,
                  shouldValidate: form.formState.isSubmitted,
                });
              }}
              error={errors.doseAmount?.message ?? errors.doseUnit?.message}
            />
          )}
        />
        <SelectField
          id="rx-route"
          label="Route"
          required
          disabled={disabled}
          hint="Only routes this medicine allows."
          error={errors.route?.message}
          {...register('route')}
        >
          <option value="">Choose route</option>
          {(medicine?.allowedRoutes ?? []).map((route) => (
            <option key={route} value={route}>
              {routeLabel(route)}
            </option>
          ))}
        </SelectField>
        <SelectField
          id="rx-frequency"
          label="Frequency"
          required
          disabled={disabled}
          error={errors.frequency?.message}
          {...register('frequency')}
        >
          <option value="">Choose how often</option>
          {FREQUENCY_OPTIONS.map((option) => (
            <option key={option.name} value={option.name}>
              {option.label}
            </option>
          ))}
        </SelectField>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Controller
          control={control}
          name="durationDays"
          render={({ field }) => (
            <QuantityInput
              id="rx-duration"
              label="Duration"
              required
              amount={field.value}
              unit="Days"
              fixedUnit="Days"
              disabled={disabled}
              onChange={(next) => field.onChange(next.amount)}
              error={errors.durationDays?.message}
            />
          )}
        />
        <Controller
          control={control}
          name="quantity"
          render={({ field }) => (
            <QuantityInput
              id="rx-quantity"
              label="Quantity per supply"
              required
              amount={field.value}
              unit={medicine?.dispensingUnit ?? ''}
              fixedUnit={medicine?.dispensingUnit ?? 'Dose'}
              disabled={disabled}
              hint="In the medicine’s dispensing unit."
              onChange={(next) => field.onChange(next.amount)}
              error={errors.quantity?.message}
            />
          )}
        />
      </div>
      <TextAreaField
        id="rx-instructions"
        label="Instructions"
        required
        rows={3}
        maxLength={1000}
        disabled={disabled}
        hint={`This text is printed on the label. ${instructions.length} / 1000`}
        error={errors.instructions?.message}
        {...register('instructions')}
      />
    </div>
  );
}
