'use client';

import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormContext } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { SelectRow, TextAreaRow } from '@/components/boarding/shared/FormBits';
import { FOOD_SOURCE_LABEL } from '@/components/boarding/shared/labels';
import {
  FOOD_SOURCES,
  MAX_FEEDINGS,
  MAX_MEDICATIONS,
  type CareProfileFormValues,
} from '@/lib/validation/boarding';

/**
 * `components/boarding/booking/CareProfileRows.tsx` — the two repeatable parts of the care profile
 * (HO-BRD-001 §8.7): feeding rows (time, portion, own or house food, description — up to 6) and
 * medication rows (name, dose, route, times, instructions, from/to — up to 10). Both read the
 * enclosing react-hook-form context so values survive a validation error (NFR-USA-5).
 */
export function FeedingRows({ idPrefix }: { idPrefix: string }) {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<CareProfileFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'feedings' });
  const listError = errors.feedings?.root?.message ?? errors.feedings?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-sm font-semibold text-ink">Feeding schedule</legend>
      {fields.map((field, index) => {
        const row = errors.feedings?.[index];
        const id = `${idPrefix}-feeding-${index}`;
        return (
          <div
            key={field.id}
            className="grid gap-3 rounded-[var(--radius-sm)] border border-border p-3 sm:grid-cols-[7rem_1fr_9rem]"
          >
            <FormFieldRow
              id={`${id}-time`}
              label="Time"
              type="time"
              error={row?.localTime?.message}
              {...register(`feedings.${index}.localTime`)}
            />
            <FormFieldRow
              id={`${id}-portion`}
              label="Portion"
              placeholder="e.g. ⅓ cup dry"
              error={row?.portion?.message}
              {...register(`feedings.${index}.portion`)}
            />
            <SelectRow
              id={`${id}-source`}
              label="Food"
              error={row?.foodSource?.message}
              {...register(`feedings.${index}.foodSource`)}
            >
              {FOOD_SOURCES.map((s) => (
                <option key={s} value={s}>
                  {FOOD_SOURCE_LABEL[s]}
                </option>
              ))}
            </SelectRow>
            <FormFieldRow
              id={`${id}-desc`}
              label="What is it?"
              placeholder="e.g. Grain-free chicken dry"
              className="sm:col-span-2"
              error={row?.foodDescription?.message}
              {...register(`feedings.${index}.foodDescription`)}
            />
            <div className="flex items-end">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => remove(index)}
                disabled={fields.length <= 1}
                aria-label={`Remove feeding ${index + 1}`}
              >
                <Trash2 size={14} aria-hidden="true" /> Remove
              </Button>
            </div>
          </div>
        );
      })}
      {listError && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {listError}
        </p>
      )}
      <div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() =>
            append({ localTime: '', portion: '', foodSource: 'OwnFood', foodDescription: '' })
          }
          disabled={fields.length >= MAX_FEEDINGS}
        >
          <Plus size={14} aria-hidden="true" /> Add a feeding
        </Button>
        <p className="mt-1 text-xs text-ink-muted">Up to {MAX_FEEDINGS} feedings a day.</p>
      </div>
    </fieldset>
  );
}

export function MedicationRows({ idPrefix }: { idPrefix: string }) {
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<CareProfileFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'medications' });
  const listError = errors.medications?.root?.message ?? errors.medications?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-sm font-semibold text-ink">Medication</legend>
      {fields.length === 0 && (
        <p className="text-sm text-ink-muted">
          No medication — add a row if {`this cat`} needs any.
        </p>
      )}
      {fields.map((field, index) => {
        const row = errors.medications?.[index];
        const id = `${idPrefix}-med-${index}`;
        return (
          <div
            key={field.id}
            className="grid gap-3 rounded-[var(--radius-sm)] border border-border p-3 sm:grid-cols-3"
          >
            <FormFieldRow
              id={`${id}-name`}
              label="Medicine"
              error={row?.name?.message}
              {...register(`medications.${index}.name`)}
            />
            <FormFieldRow
              id={`${id}-dose`}
              label="Dose"
              placeholder="e.g. 0.5 ml"
              error={row?.dose?.message}
              {...register(`medications.${index}.dose`)}
            />
            <FormFieldRow
              id={`${id}-route`}
              label="Route"
              placeholder="e.g. Oral"
              error={row?.route?.message}
              {...register(`medications.${index}.route`)}
            />
            <FormFieldRow
              id={`${id}-times`}
              label="Times"
              placeholder="08:00, 20:00"
              error={row?.times?.message}
              {...register(`medications.${index}.times`)}
            />
            <FormFieldRow
              id={`${id}-from`}
              label="From (optional)"
              type="date"
              {...register(`medications.${index}.fromOn`)}
            />
            <FormFieldRow
              id={`${id}-to`}
              label="To (optional)"
              type="date"
              {...register(`medications.${index}.toOn`)}
            />
            <TextAreaRow
              id={`${id}-instructions`}
              label="Instructions"
              rows={2}
              className="sm:col-span-3"
              error={row?.instructions?.message}
              {...register(`medications.${index}.instructions`)}
            />
            <div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => remove(index)}
                aria-label={`Remove medicine ${index + 1}`}
              >
                <Trash2 size={14} aria-hidden="true" /> Remove
              </Button>
            </div>
          </div>
        );
      })}
      {listError && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {listError}
        </p>
      )}
      <div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() =>
            append({
              name: '',
              dose: '',
              route: '',
              times: '',
              instructions: '',
              fromOn: '',
              toOn: '',
            })
          }
          disabled={fields.length >= MAX_MEDICATIONS}
        >
          <Plus size={14} aria-hidden="true" /> Add medication
        </Button>
      </div>
    </fieldset>
  );
}
