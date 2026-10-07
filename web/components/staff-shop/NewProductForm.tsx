'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import {
  applyValidationErrors,
  describeUnexpectedError,
  SHOP_CODE_MESSAGES,
  SHOP_FIELD_MAP,
} from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { categoryLabel, subcategoryLabel } from '@/lib/shop/labels';
import type { CategoryNode } from '@/lib/api/shop-catalogue';
import type { TaxClass } from '@/lib/api/admin-catalogue';
import type { CreateProductRequest } from '@/lib/api/staff-shop';
import { Field, inputClass } from './SectionFrame';

const schema = z
  .object({
    kind: z.enum(['Standard', 'Bundle']),
    name: z.string().trim().min(1, 'Enter the product name.').max(200),
    brand: z.string().trim().min(1, 'Enter the brand.').max(100),
    category: z.string().min(1, 'Choose a category.'),
    subcategory: z.string(),
    taxClassId: z.string().min(1, 'Choose a tax class.'),
    slug: z
      .string()
      .trim()
      .min(1, 'Enter the web address.')
      .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Use lower-case letters, numbers and single hyphens.'),
    bundleSkuCode: z.string().trim(),
  })
  .refine((v) => v.kind !== 'Bundle' || v.bundleSkuCode.length > 0, {
    path: ['bundleSkuCode'],
    message: 'Enter a SKU code for the kit.',
  });
type FormValues = z.infer<typeof schema>;
const KNOWN = [
  'kind',
  'name',
  'brand',
  'category',
  'subcategory',
  'taxClassId',
  'slug',
  'bundleSkuCode',
] as const;

export function suggestSlug(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

export interface NewProductFormProps {
  categories: CategoryNode[];
  taxClasses: TaxClass[];
  onCreate: (request: CreateProductRequest) => Promise<unknown>;
}

/** HO-SHP-002 §11.6 `NewProductForm` — kind (Standard / Kit), name, brand, category → subcategory
 *  (only the category's), tax class, slug (suggested from the name, editable); a kit also asks for
 *  its SKU code. */
export function NewProductForm({ categories, taxClasses, onCreate }: NewProductFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    watch,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful, dirtyFields },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      kind: 'Standard',
      name: '',
      brand: '',
      category: '',
      subcategory: '',
      taxClassId: '',
      slug: '',
      bundleSkuCode: '',
    },
  });
  const kind = watch('kind');
  const category = watch('category');
  const subcategories = categories.find((c) => c.value === category)?.subcategories ?? [];

  async function onSubmit(values: FormValues) {
    try {
      await onCreate({
        kind: values.kind,
        name: values.name,
        brand: values.brand,
        category: values.category,
        subcategory: values.subcategory || null,
        taxClassId: values.taxClassId,
        slug: values.slug,
        bundleSkuCode: values.kind === 'Bundle' ? values.bundleSkuCode : null,
      });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: SHOP_FIELD_MAP as never,
          codeMessages: SHOP_CODE_MESSAGES,
          knownFields: KNOWN,
        });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  const nameField = register('name');
  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-xl flex-col gap-4">
      <UnsavedChangesGuard when={isDirty && !isSubmitting && !isSubmitSuccessful} />
      <h1 className="font-display text-2xl font-semibold text-ink">New product</h1>
      {errors.root && (
        <p
          role="alert"
          className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          {errors.root.message}
        </p>
      )}
      <Field id="np-kind" label="Kind" error={errors.kind?.message}>
        <select id="np-kind" className={inputClass} {...register('kind')}>
          <option value="Standard">Standard</option>
          <option value="Bundle">Kit (bundle)</option>
        </select>
      </Field>
      <Field id="np-name" label="Name" error={errors.name?.message}>
        <input
          id="np-name"
          className={inputClass}
          aria-invalid={!!errors.name}
          {...nameField}
          onChange={(event) => {
            void nameField.onChange(event);
            if (!dirtyFields.slug) setValue('slug', suggestSlug(event.target.value));
          }}
        />
      </Field>
      <Field id="np-brand" label="Brand" error={errors.brand?.message}>
        <input
          id="np-brand"
          className={inputClass}
          aria-invalid={!!errors.brand}
          {...register('brand')}
        />
      </Field>
      <Field id="np-category" label="Category" error={errors.category?.message}>
        <select
          id="np-category"
          className={inputClass}
          aria-invalid={!!errors.category}
          {...register('category', { onChange: () => setValue('subcategory', '') })}
        >
          <option value="">Choose…</option>
          {categories.map((c) => (
            <option key={c.value} value={c.value}>
              {categoryLabel(c.value)}
            </option>
          ))}
        </select>
      </Field>
      {subcategories.length > 0 && (
        <Field id="np-sub" label="Subcategory" error={errors.subcategory?.message}>
          <select id="np-sub" className={inputClass} {...register('subcategory')}>
            <option value="">None</option>
            {subcategories.map((s) => (
              <option key={s.value} value={s.value}>
                {subcategoryLabel(s.value)}
              </option>
            ))}
          </select>
        </Field>
      )}
      <Field id="np-tax" label="Tax class" error={errors.taxClassId?.message}>
        <select
          id="np-tax"
          className={inputClass}
          aria-invalid={!!errors.taxClassId}
          {...register('taxClassId')}
        >
          <option value="">Choose…</option>
          {taxClasses
            .filter((t) => t.isActive)
            .map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
        </select>
      </Field>
      <Field
        id="np-slug"
        label="Web address (slug)"
        error={errors.slug?.message}
        hint="Suggested from the name. It can't be changed after the product is first published."
      >
        <input
          id="np-slug"
          className={inputClass}
          aria-invalid={!!errors.slug}
          {...register('slug')}
        />
      </Field>
      {kind === 'Bundle' && (
        <Field id="np-kit" label="Kit SKU code" error={errors.bundleSkuCode?.message}>
          <input
            id="np-kit"
            className={inputClass}
            aria-invalid={!!errors.bundleSkuCode}
            {...register('bundleSkuCode')}
          />
        </Field>
      )}
      <Button type="submit" disabled={isSubmitting} className="self-start">
        {isSubmitting ? 'Creating…' : 'Create product'}
      </Button>
    </form>
  );
}
