'use client';

import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { submitSection } from '@/lib/shop/submitSection';
import type { StaffImage, StaffProductDetail } from '@/lib/api/staff-shop';
import { Field, SectionFrame, inputClass } from './SectionFrame';

const row = z.object({
  fileId: z
    .string()
    .trim()
    .regex(/^[0-9a-fA-F-]{36}$/, 'Enter the stored file’s id (a UUID).'),
  skuId: z.string(),
  altText: z.string().trim().min(1, 'Describe the image in a short alt text.').max(200),
});
const schema = z.object({
  images: z.array(row).max(12, 'A product can have up to 12 images.'),
  primary: z.string(),
});
type Values = z.infer<typeof schema>;

export interface ImagesEditorProps {
  product: StaffProductDetail;
  onSave: (
    images: Array<Omit<StaffImage, 'sortOrder'> & { sortOrder: number }>,
  ) => Promise<unknown>;
  onReload: () => void;
}

/**
 * HO-SHP-002 §11.6 `ImagesEditor`, ADR-FE-035 — an ordered list; each image has the stored file's id
 * (the API takes a `fileId` from File Storage), a product-level or per-SKU scope, **alt text
 * (required)** and a primary radio. **There is no upload here** (HO-SHP-001 ADR-091; §4): the honest
 * note says so. *Save replaces the set*; the list starts from the images the staff read returns
 * (HO-SHP-001-FU1), in order, so saving unchanged keeps them.
 */
export function ImagesEditor({ product, onSave, onReload }: ImagesEditorProps) {
  const existing = product.images;
  const {
    register,
    control,
    handleSubmit,
    setError,
    reset,
    watch,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      images: existing.map((i) => ({ fileId: i.fileId, skuId: i.skuId ?? '', altText: i.altText })),
      primary: String(
        Math.max(
          0,
          existing.findIndex((i) => i.isPrimary),
        ),
      ),
    },
  });
  const { fields, append, remove, move } = useFieldArray({ control, name: 'images' });
  const primary = watch('primary');

  return (
    <SectionFrame
      title="Images"
      description={
        <>Image upload isn&apos;t available yet — enter the stored file&apos;s key (its file id).</>
      }
      errors={errors}
      isDirty={isDirty}
      isSubmitting={isSubmitting}
      isSubmitSuccessful={isSubmitSuccessful}
      onReload={onReload}
      onSubmit={handleSubmit(async (values) => {
        const ok = await submitSection(
          () =>
            onSave(
              values.images.map((image, index) => ({
                fileId: image.fileId,
                skuId: image.skuId || null,
                altText: image.altText,
                sortOrder: index,
                isPrimary: String(index) === values.primary,
              })),
            ),
          setError,
          ['images'],
        );
        if (ok) reset(values);
      })}
    >
      <ol className="m-0 flex list-none flex-col gap-3 p-0">
        {fields.map((field, index) => (
          <li
            key={field.id}
            className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3"
          >
            <p className="text-sm font-bold text-ink">Image {index + 1}</p>
            <Field
              id={`img-${index}-file`}
              label="Stored file id"
              error={errors.images?.[index]?.fileId?.message}
            >
              <input
                id={`img-${index}-file`}
                className={inputClass}
                aria-invalid={!!errors.images?.[index]?.fileId}
                {...register(`images.${index}.fileId`)}
              />
            </Field>
            <Field
              id={`img-${index}-alt`}
              label="Alt text (required)"
              error={errors.images?.[index]?.altText?.message}
            >
              <input
                id={`img-${index}-alt`}
                className={inputClass}
                aria-invalid={!!errors.images?.[index]?.altText}
                {...register(`images.${index}.altText`)}
              />
            </Field>
            <Field id={`img-${index}-scope`} label="Shown for">
              <select
                id={`img-${index}-scope`}
                className={inputClass}
                {...register(`images.${index}.skuId`)}
              >
                <option value="">The whole product</option>
                {product.skus.map((sku) => (
                  <option key={sku.skuId} value={sku.skuId}>
                    {sku.code}
                  </option>
                ))}
              </select>
            </Field>
            <div className="flex flex-wrap items-center gap-3">
              <label className="flex items-center gap-1.5 text-sm text-ink">
                <input
                  type="radio"
                  value={String(index)}
                  checked={primary === String(index)}
                  className="h-4 w-4 accent-[var(--accent)]"
                  {...register('primary')}
                />
                Primary image
              </label>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={index === 0}
                onClick={() => move(index, index - 1)}
              >
                Move up
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={index === fields.length - 1}
                onClick={() => move(index, index + 1)}
              >
                Move down
              </Button>
              <Button type="button" variant="ghost" size="sm" onClick={() => remove(index)}>
                Remove
              </Button>
            </div>
          </li>
        ))}
      </ol>
      {errors.images?.message && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {errors.images.message}
        </p>
      )}
      <Button
        type="button"
        variant="secondary"
        className="self-start"
        onClick={() => append({ fileId: '', skuId: '', altText: '' })}
      >
        Add image
      </Button>
    </SectionFrame>
  );
}
