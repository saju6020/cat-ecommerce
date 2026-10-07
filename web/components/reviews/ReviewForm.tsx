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

export const REVIEW_BODY_MAX = 2000;
const COUNTER_ANNOUNCE_AT = 100;

const schema = z.object({
  skuId: z.string().min(1, 'Choose the size you bought.'),
  rating: z.string().regex(/^[1-5]$/, 'Choose a rating from 1 to 5.'),
  title: z.string().trim().max(100, 'Keep the title under 100 characters.'),
  body: z
    .string()
    .trim()
    .min(1, 'Write a few words about the product.')
    .max(REVIEW_BODY_MAX, `Keep the review under ${REVIEW_BODY_MAX} characters.`),
});
type FormValues = z.infer<typeof schema>;
const KNOWN_FIELDS = ['skuId', 'rating', 'title', 'body'] as const;

export interface ReviewFormProps {
  productName: string;
  /** The variants the customer bought (`review-eligibility.purchasedSkus`). */
  purchasedSkus: Array<{ skuId: string; label: string }>;
  initial?: { skuId: string; rating: number; title: string | null; body: string };
  /** Editing keeps the original variant. */
  isEdit?: boolean;
  onSave: (values: {
    skuId: string;
    rating: number;
    title: string | null;
    body: string;
  }) => Promise<unknown>;
}

/**
 * HO-SHP-002 §11.5 `ReviewForm` — US-SHP-09. Rating is a radio group with text labels; title and
 * body are PLAIN TEXT (2000 characters, the counter announced at 100 remaining). Values survive a
 * server refusal (`ReviewNotPermitted`, `AlreadyReviewed` show in plain language) and an unsaved
 * form guards navigation (NFR-USA-5).
 */
export function ReviewForm({
  productName,
  purchasedSkus,
  initial,
  isEdit,
  onSave,
}: ReviewFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    watch,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      skuId: initial?.skuId ?? purchasedSkus[0]?.skuId ?? '',
      rating: initial ? String(initial.rating) : '',
      title: initial?.title ?? '',
      body: initial?.body ?? '',
    },
  });
  const remaining = REVIEW_BODY_MAX - (watch('body')?.length ?? 0);

  async function onSubmit(values: FormValues) {
    try {
      await onSave({
        skuId: values.skuId,
        rating: Number(values.rating),
        title: values.title || null,
        body: values.body,
      });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: SHOP_FIELD_MAP as never,
          codeMessages: SHOP_CODE_MESSAGES,
          knownFields: KNOWN_FIELDS,
        });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  const input =
    'w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink aria-[invalid=true]:border-danger-ink';
  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-xl flex-col gap-4">
      <UnsavedChangesGuard when={isDirty && !isSubmitting && !isSubmitSuccessful} />
      <h1 className="font-display text-2xl font-semibold text-ink">
        {isEdit ? 'Edit your review' : 'Write a review'} of {productName}
      </h1>

      {errors.root && (
        <p
          role="alert"
          className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          {errors.root.message}
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="review-sku" className="text-sm font-semibold text-ink">
          Size you bought
        </label>
        <select
          id="review-sku"
          className={input}
          aria-invalid={!!errors.skuId}
          {...register('skuId')}
        >
          {purchasedSkus.map((sku) => (
            <option key={sku.skuId} value={sku.skuId}>
              {sku.label}
            </option>
          ))}
        </select>
        {errors.skuId && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.skuId.message}
          </p>
        )}
      </div>

      <fieldset className="m-0 border-0 p-0" aria-invalid={!!errors.rating}>
        <legend className="mb-1 p-0 text-sm font-semibold text-ink">Rating</legend>
        <div className="flex flex-wrap gap-3">
          {[1, 2, 3, 4, 5].map((n) => (
            <label key={n} className="flex items-center gap-1.5 text-sm text-ink">
              <input
                type="radio"
                value={String(n)}
                className="h-4 w-4 accent-[var(--accent)]"
                {...register('rating')}
              />
              {n} out of 5
            </label>
          ))}
        </div>
        {errors.rating && (
          <p role="alert" className="mt-1 text-xs font-semibold text-danger-ink">
            {errors.rating.message}
          </p>
        )}
      </fieldset>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="review-title" className="text-sm font-semibold text-ink">
          Title <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="review-title"
          className={input}
          aria-invalid={!!errors.title}
          {...register('title')}
        />
        {errors.title && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.title.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="review-body" className="text-sm font-semibold text-ink">
          Your review
        </label>
        <textarea
          id="review-body"
          rows={6}
          className={input}
          aria-invalid={!!errors.body}
          aria-describedby="review-count"
          {...register('body')}
        />
        <p id="review-count" className="text-xs text-ink-muted">
          {remaining} characters left
        </p>
        <p role="status" aria-live="polite" className="sr-only">
          {remaining <= COUNTER_ANNOUNCE_AT ? `${remaining} characters left` : ''}
        </p>
        {errors.body && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.body.message}
          </p>
        )}
      </div>

      <p className="text-sm text-ink-muted">Reviews are checked before they appear.</p>
      <Button type="submit" disabled={isSubmitting} className="self-start">
        {isSubmitting ? 'Sending…' : isEdit ? 'Save changes' : 'Send review'}
      </Button>
    </form>
  );
}
