'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { FieldWithUnit } from '@/components/common/FieldWithUnit';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { submitSection } from '@/lib/shop/submitSection';
import { lifeStageLabel, proteinLabel } from '@/lib/shop/labels';
import type { StaffFoodDetail, StaffProductDetail } from '@/lib/api/staff-shop';
import { Field, SectionFrame, inputClass } from './SectionFrame';

const LIFE_STAGES = ['Kitten', 'Adult', 'Senior', 'AllLifeStages'];
const PROTEINS = [
  'Chicken',
  'Turkey',
  'Duck',
  'Beef',
  'Lamb',
  'Fish',
  'Tuna',
  'Salmon',
  'Rabbit',
  'Insect',
  'PlantBased',
  'Mixed',
  'Other',
];

const percent = z
  .string()
  .refine(
    (v) => v === '' || (Number.isFinite(Number(v)) && Number(v) >= 0 && Number(v) <= 100),
    'Enter a number from 0 to 100.',
  );
const schema = z
  .object({
    manufacturer: z.string().trim().max(200),
    lifeStage: z.string(),
    proteinSource: z.string(),
    ingredientList: z.string().max(4000),
    proteinPercent: percent,
    fatPercent: percent,
    fibrePercent: percent,
    moisturePercent: percent,
    feedingGuide: z.string().max(5000),
    storageInstructions: z.string().max(1000),
    prescriptionRequired: z.boolean(),
  })
  .superRefine((v, ctx) => {
    const values = [v.proteinPercent, v.fatPercent, v.fibrePercent, v.moisturePercent];
    const filled = values.filter((x) => x !== '').length;
    if (filled > 0 && filled < 4) {
      ctx.addIssue({
        code: 'custom',
        path: ['proteinPercent'],
        message: 'Enter all four analysis values, or leave all four empty.',
      });
    }
  });
export type FoodFormValues = z.infer<typeof schema>;
const KNOWN = [
  'manufacturer',
  'lifeStage',
  'proteinSource',
  'ingredientList',
  'proteinPercent',
  'fatPercent',
  'fibrePercent',
  'moisturePercent',
  'feedingGuide',
  'storageInstructions',
  'prescriptionRequired',
] as const;

function initial(food: StaffFoodDetail | null | undefined, rx: boolean): FoodFormValues {
  const a = food?.guaranteedAnalysis;
  return {
    manufacturer: food?.manufacturer ?? '',
    lifeStage: food?.lifeStage ?? '',
    proteinSource: food?.proteinSource ?? '',
    ingredientList: food?.ingredientList ?? '',
    proteinPercent: a ? String(a.proteinPercent) : '',
    fatPercent: a ? String(a.fatPercent) : '',
    fibrePercent: a ? String(a.fibrePercent) : '',
    moisturePercent: a ? String(a.moisturePercent) : '',
    feedingGuide: food?.feedingGuide ?? '',
    storageInstructions: food?.storageInstructions ?? '',
    prescriptionRequired: rx,
  };
}

export interface FoodDetailsFormProps {
  product: StaffProductDetail;
  onSave: (values: FoodFormValues) => Promise<unknown>;
  onReload: () => void;
}

/**
 * HO-SHP-002 §11.6 `FoodDetailsForm` — US-SHP-03, Cat Food only (the page does not offer it for any
 * other category). Guaranteed analysis is four inputs, each with a visible `%` unit; ingredient list
 * and storage are PLAIN TEXT, feeding guide is LimitedMarkup with a live preview. Ticking
 * **Prescription diet** asks for confirmation stating that customers will need an authorised cat.
 * The form starts from the stored labelling returned by the staff read (HO-SHP-001-FU1).
 */
export function FoodDetailsForm({ product, onSave, onReload }: FoodDetailsFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    watch,
    reset,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FoodFormValues>({
    resolver: zodResolver(schema),
    defaultValues: initial(product.food, product.prescriptionRequired),
  });
  const [confirmRx, setConfirmRx] = React.useState(false);
  const feedingGuide = watch('feedingGuide');
  const rx = watch('prescriptionRequired');

  return (
    <>
      <SectionFrame
        title="Food & labelling"
        errors={errors}
        isDirty={isDirty}
        isSubmitting={isSubmitting}
        isSubmitSuccessful={isSubmitSuccessful}
        onReload={onReload}
        onSubmit={handleSubmit(async (values) => {
          const ok = await submitSection(() => onSave(values), setError, KNOWN);
          if (ok) reset(values);
        })}
      >
        <Field id="fd-manufacturer" label="Manufacturer" error={errors.manufacturer?.message}>
          <input id="fd-manufacturer" className={inputClass} {...register('manufacturer')} />
        </Field>
        <div className="grid gap-4 md:grid-cols-2">
          <Field
            id="fd-stage"
            label="Life stage"
            error={errors.lifeStage?.message}
            hint="Needed to publish — it drives filtering and suggestions."
          >
            <select id="fd-stage" className={inputClass} {...register('lifeStage')}>
              <option value="">Not set</option>
              {LIFE_STAGES.map((s) => (
                <option key={s} value={s}>
                  {lifeStageLabel(s)}
                </option>
              ))}
            </select>
          </Field>
          <Field id="fd-protein" label="Protein source" error={errors.proteinSource?.message}>
            <select id="fd-protein" className={inputClass} {...register('proteinSource')}>
              <option value="">Not set</option>
              {PROTEINS.map((p) => (
                <option key={p} value={p}>
                  {proteinLabel(p)}
                </option>
              ))}
            </select>
          </Field>
        </div>
        <Field
          id="fd-ingredients"
          label="Ingredient list"
          error={errors.ingredientList?.message}
          hint="Plain text."
        >
          <textarea
            id="fd-ingredients"
            rows={3}
            className={inputClass}
            {...register('ingredientList')}
          />
        </Field>
        <fieldset className="m-0 border-0 p-0">
          <legend className="mb-2 p-0 text-sm font-semibold text-ink">Guaranteed analysis</legend>
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <FieldWithUnit
              id="fd-protein-pct"
              label="Protein"
              unit="%"
              step="0.1"
              error={errors.proteinPercent?.message}
              {...register('proteinPercent')}
            />
            <FieldWithUnit
              id="fd-fat-pct"
              label="Fat"
              unit="%"
              step="0.1"
              error={errors.fatPercent?.message}
              {...register('fatPercent')}
            />
            <FieldWithUnit
              id="fd-fibre-pct"
              label="Fibre"
              unit="%"
              step="0.1"
              error={errors.fibrePercent?.message}
              {...register('fibrePercent')}
            />
            <FieldWithUnit
              id="fd-moisture-pct"
              label="Moisture"
              unit="%"
              step="0.1"
              error={errors.moisturePercent?.message}
              {...register('moisturePercent')}
            />
          </div>
        </fieldset>
        <div className="grid gap-4 md:grid-cols-2">
          <Field
            id="fd-feeding"
            label="Feeding guide"
            error={errors.feedingGuide?.message}
            hint="Paragraphs, ## headings, - bullets, **bold** and links only."
          >
            <textarea
              id="fd-feeding"
              rows={6}
              className={inputClass}
              {...register('feedingGuide')}
            />
          </Field>
          <div className="flex flex-col gap-1.5">
            <p className="text-sm font-semibold text-ink">Preview</p>
            <div
              role="group"
              aria-label="Feeding guide preview"
              className="min-h-24 rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
            >
              <LimitedMarkup body={feedingGuide || ''} className="space-y-2" />
            </div>
          </div>
        </div>
        <Field
          id="fd-storage"
          label="Storage instructions"
          error={errors.storageInstructions?.message}
          hint="Plain text."
        >
          <textarea
            id="fd-storage"
            rows={2}
            className={inputClass}
            {...register('storageInstructions')}
          />
        </Field>
        <label className="flex items-start gap-2 text-sm text-ink">
          <input
            type="checkbox"
            className="mt-0.5 h-4 w-4 accent-[var(--accent)]"
            checked={rx}
            onChange={(event) => {
              if (event.target.checked) setConfirmRx(true);
              else setValue('prescriptionRequired', false, { shouldDirty: true });
            }}
          />
          <span>Prescription diet — requires vet authorisation</span>
        </label>
      </SectionFrame>
      <ConfirmDialog
        open={confirmRx}
        onOpenChange={setConfirmRx}
        title="Make this a prescription diet?"
        consequence="Customers will need a cat with a current vet authorisation to buy this food. Bundles can't contain it."
        confirmLabel="Require authorisation"
        onConfirm={() => {
          setValue('prescriptionRequired', true, { shouldDirty: true });
          setConfirmRx(false);
        }}
      />
    </>
  );
}
