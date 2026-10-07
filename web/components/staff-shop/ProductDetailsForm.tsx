'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { submitSection } from '@/lib/shop/submitSection';
import { categoryLabel, subcategoryLabel } from '@/lib/shop/labels';
import type { CategoryNode } from '@/lib/api/shop-catalogue';
import type { TaxClass } from '@/lib/api/admin-catalogue';
import type { StaffProductDetail } from '@/lib/api/staff-shop';
import { Field, SectionFrame, inputClass } from './SectionFrame';

export const HEALTH_FOCUSES = [
  'Sterilised',
  'WeightManagement',
  'Urinary',
  'Renal',
  'Digestive',
  'SkinAndCoat',
  'Hairball',
  'Dental',
  'Sensitive',
] as const;

const schema = z.object({
  name: z.string().trim().min(1, 'Enter the product name.').max(200),
  brand: z.string().trim().min(1, 'Enter the brand.').max(100),
  description: z.string().max(5000, 'Keep the description under 5000 characters.'),
  subcategory: z.string(),
  tags: z.string(),
  countryOfOriginCode: z.string(),
  taxClassId: z.string().min(1, 'Choose a tax class.'),
  listedOnStorefront: z.boolean(),
  isExpiryTracked: z.boolean(),
  healthFocus: z.array(z.string()),
  slug: z.string().trim().min(1, 'Enter the web address.'),
});
export type DetailsFormValues = z.infer<typeof schema>;
const KNOWN = [
  'name',
  'brand',
  'description',
  'subcategory',
  'tags',
  'countryOfOriginCode',
  'taxClassId',
  'listedOnStorefront',
  'isExpiryTracked',
  'healthFocus',
  'slug',
] as const;

/** Tags are normalised the way the server stores them: trimmed, lower-case, de-duplicated. */
export function normaliseTags(text: string): string[] {
  return [
    ...new Set(
      text
        .split(',')
        .map((t) => t.trim().toLowerCase())
        .filter(Boolean),
    ),
  ];
}

export interface ProductDetailsFormProps {
  product: StaffProductDetail;
  categories: CategoryNode[];
  taxClasses: TaxClass[];
  countries: Array<{ code: string; name: string }>;
  onSave: (values: DetailsFormValues & { tagList: string[] }) => Promise<unknown>;
  onReload: () => void;
}

/**
 * HO-SHP-002 §11.6 `ProductDetailsForm` — US-SHP-01. Description is LimitedMarkup with a live
 * preview through the SHARED renderer (what staff see is what the shop shows). The slug is read-only
 * once the product has been published; the category is read-only while the product is active.
 * *Track expiry* is locked on for food. Health focus (food) is saved with the details and loaded from
 * the staff read (HO-SHP-001-FU1), so a save that does not touch it sends back what is stored.
 */
export function ProductDetailsForm({
  product,
  categories,
  taxClasses,
  countries,
  onSave,
  onReload,
}: ProductDetailsFormProps) {
  const isFood = product.category === 'CatFood' && product.kind === 'Standard';
  const {
    register,
    handleSubmit,
    setError,
    watch,
    reset,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<DetailsFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: product.name,
      brand: product.brand,
      description: product.description,
      subcategory: product.subcategory ?? '',
      tags: product.tags.join(', '),
      countryOfOriginCode: product.countryOfOriginCode ?? '',
      taxClassId: product.taxClassId,
      listedOnStorefront: product.listedOnStorefront,
      isExpiryTracked: isFood ? true : product.isExpiryTracked,
      healthFocus: product.healthFocus ?? [],
      slug: product.slug,
    },
  });
  const description = watch('description');
  const tags = normaliseTags(watch('tags') ?? '');
  const subcategories = categories.find((c) => c.value === product.category)?.subcategories ?? [];
  const slugLocked = !!product.firstPublishedOn;

  return (
    <SectionFrame
      title="Details"
      errors={errors}
      isDirty={isDirty}
      isSubmitting={isSubmitting}
      isSubmitSuccessful={isSubmitSuccessful}
      onReload={onReload}
      onSubmit={handleSubmit(async (values) => {
        const ok = await submitSection(
          () => onSave({ ...values, tagList: normaliseTags(values.tags) }),
          setError,
          KNOWN,
        );
        if (ok) reset(values);
      })}
    >
      <Field id="pd-name" label="Name" error={errors.name?.message}>
        <input
          id="pd-name"
          className={inputClass}
          aria-invalid={!!errors.name}
          {...register('name')}
        />
      </Field>
      <Field id="pd-brand" label="Brand" error={errors.brand?.message}>
        <input
          id="pd-brand"
          className={inputClass}
          aria-invalid={!!errors.brand}
          {...register('brand')}
        />
      </Field>
      <Field
        id="pd-cat"
        label="Category"
        hint={
          product.status === 'Active'
            ? 'The category is locked while the product is active.'
            : undefined
        }
      >
        <select
          id="pd-cat"
          className={inputClass}
          disabled
          value={product.category}
          onChange={() => undefined}
        >
          <option value={product.category}>{categoryLabel(product.category)}</option>
        </select>
      </Field>
      {subcategories.length > 0 && (
        <Field id="pd-sub" label="Subcategory" error={errors.subcategory?.message}>
          <select id="pd-sub" className={inputClass} {...register('subcategory')}>
            <option value="">None</option>
            {subcategories.map((s) => (
              <option key={s.value} value={s.value}>
                {subcategoryLabel(s.value)}
              </option>
            ))}
          </select>
        </Field>
      )}
      <div className="grid gap-4 md:grid-cols-2">
        <Field
          id="pd-desc"
          label="Description"
          error={errors.description?.message}
          hint="Paragraphs, ## headings, - bullets, **bold** and [links](https://…) only."
        >
          <textarea
            id="pd-desc"
            rows={8}
            className={inputClass}
            aria-invalid={!!errors.description}
            {...register('description')}
          />
        </Field>
        <div className="flex flex-col gap-1.5">
          <p className="text-sm font-semibold text-ink">Preview</p>
          <div
            role="group"
            aria-label="Description preview"
            className="min-h-24 rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
          >
            <LimitedMarkup body={description || ''} className="space-y-2" />
          </div>
        </div>
      </div>
      <Field
        id="pd-tags"
        label="Tags"
        hint={`Separate with commas. Saved as: ${tags.length ? tags.join(', ') : 'none'}`}
      >
        <input id="pd-tags" className={inputClass} {...register('tags')} />
      </Field>
      <Field
        id="pd-country"
        label="Country of origin"
        hint={isFood ? 'Required to publish a food product.' : undefined}
      >
        <select id="pd-country" className={inputClass} {...register('countryOfOriginCode')}>
          <option value="">Not set</option>
          {countries.map((c) => (
            <option key={c.code} value={c.code}>
              {c.name}
            </option>
          ))}
        </select>
      </Field>
      <Field id="pd-tax" label="Tax class" error={errors.taxClassId?.message}>
        <select id="pd-tax" className={inputClass} {...register('taxClassId')}>
          {taxClasses.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
      </Field>
      <label className="flex items-center gap-2 text-sm text-ink">
        <input
          type="checkbox"
          className="h-4 w-4 accent-[var(--accent)]"
          {...register('listedOnStorefront')}
        />
        Show in online shop
      </label>
      <label className="flex items-center gap-2 text-sm text-ink">
        <input
          type="checkbox"
          className="h-4 w-4 accent-[var(--accent)]"
          disabled={isFood}
          {...register('isExpiryTracked')}
        />
        Track expiry{isFood ? ' (always on for food)' : ''}
      </label>
      {isFood && (
        <fieldset className="m-0 border-0 p-0">
          <legend className="mb-1 p-0 text-sm font-semibold text-ink">Health focus</legend>
          <p className="mb-2 text-xs text-ink-muted">Saved with the details.</p>
          <div className="flex flex-wrap gap-x-4 gap-y-1.5">
            {HEALTH_FOCUSES.map((focus) => (
              <label key={focus} className="flex items-center gap-1.5 text-sm text-ink">
                <input
                  type="checkbox"
                  value={focus}
                  className="h-4 w-4 accent-[var(--accent)]"
                  {...register('healthFocus')}
                />
                {subcategoryLabel(focus)}
              </label>
            ))}
          </div>
        </fieldset>
      )}
      <Field
        id="pd-slug"
        label="Web address (slug)"
        error={errors.slug?.message}
        hint={
          slugLocked
            ? 'The web address can’t change once a product has been published.'
            : 'Can be changed until the first publication.'
        }
      >
        <input
          id="pd-slug"
          className={inputClass}
          readOnly={slugLocked}
          aria-invalid={!!errors.slug}
          {...register('slug')}
        />
      </Field>
    </SectionFrame>
  );
}
