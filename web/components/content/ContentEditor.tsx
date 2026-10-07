'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { LimitedMarkup } from './LimitedMarkup';
import { applyValidationErrors, describeApiError, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { BANNER_PLACEMENTS, type ContentItem, type ContentKind } from '@/lib/api/admin-content';

function slugify(title: string): string {
  return title
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

const schema = z.object({
  title: z.string().trim().min(1, 'Enter a title.').max(200),
  slug: z
    .string()
    .trim()
    .min(1, 'Enter a slug.')
    .max(80)
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, 'Use lower-case letters, digits and single hyphens only.'),
  draftBody: z.string().max(50_000),
  placement: z.enum(['Home', 'Shop', 'Portal']).optional(),
  linkUrl: z.string().trim().optional(),
  visibleFrom: z.string().optional(),
  visibleTo: z.string().optional(),
  // Plain z.number(), not z.coerce.number() (see WeightEntryForm.tsx's comment on why) —
  // register('sortOrder', { valueAsNumber: true }) below does the conversion instead.
  sortOrder: z.number().optional(),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  Title: 'title',
  Slug: 'slug',
  DraftBody: 'draftBody',
  Placement: 'placement',
  LinkUrl: 'linkUrl',
  VisibleFrom: 'visibleFrom',
  VisibleTo: 'visibleTo',
  SortOrder: 'sortOrder',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

const MARKUP_HELP = [
  'A blank line starts a new paragraph.',
  '## Heading and ### Subheading',
  '- a bullet point',
  '**bold text**',
  '[link text](https://example.com or /a-site-page)',
].join('\n');

/**
 * `components/content/ContentEditor.tsx` — ⭐ HO-ADM-002 §11.6, US-ADM-07. Slug is auto-suggested
 * from the title and stays editable until the item has been published once (`publishedOn` set) —
 * after that the published URL must not move. The preview uses the exact same `LimitedMarkup`
 * component the public page renders (U-F2).
 */
export interface ContentEditorProps {
  kind: ContentKind;
  item?: ContentItem;
  onCreate?: (values: FormValues) => Promise<unknown>;
  onSaveDraft?: (values: FormValues) => Promise<unknown>;
  onPublish?: () => Promise<unknown>;
  onUnpublish?: () => Promise<unknown>;
  isPending: boolean;
}

export function ContentEditor({
  kind,
  item,
  onCreate,
  onSaveDraft,
  onPublish,
  onUnpublish,
  isPending,
}: ContentEditorProps) {
  const isCreate = !item;
  const slugLocked = !!item?.publishedOn;
  const [publishConfirmOpen, setPublishConfirmOpen] = useState(false);
  const [unpublishConfirmOpen, setUnpublishConfirmOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors, isDirty, isSubmitSuccessful, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: item?.title ?? '',
      slug: item?.slug ?? '',
      draftBody: item?.draftBody ?? '',
      placement: item?.placement ?? undefined,
      linkUrl: item?.linkUrl ?? '',
      visibleFrom: item?.visibleFrom ?? '',
      visibleTo: item?.visibleTo ?? '',
      sortOrder: item?.sortOrder ?? undefined,
    },
  });

  const title = watch('title');
  const body = watch('draftBody');

  function handleTitleChange(value: string) {
    setValue('title', value);
    if (!slugLocked && isCreate) setValue('slug', slugify(value));
  }

  async function onSubmit(values: FormValues) {
    try {
      if (isCreate) await onCreate?.(values);
      else await onSaveDraft?.(values);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  async function handlePublish() {
    setActionError(null);
    try {
      await onPublish?.();
      setPublishConfirmOpen(false);
    } catch (err) {
      setActionError(describeApiError(err));
    }
  }

  async function handleUnpublish() {
    setActionError(null);
    try {
      await onUnpublish?.();
      setUnpublishConfirmOpen(false);
    } catch (err) {
      setActionError(describeApiError(err));
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-6">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />

      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {actionError && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {actionError}
        </p>
      )}
      {isSubmitSuccessful && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Draft saved.
        </p>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <label htmlFor="content-title" className="text-sm font-semibold text-ink">
              Title
            </label>
            <input
              id="content-title"
              value={title}
              onChange={(e) => handleTitleChange(e.target.value)}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.title && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.title.message}
              </p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="content-slug" className="text-sm font-semibold text-ink">
              Slug
            </label>
            <input
              id="content-slug"
              readOnly={slugLocked}
              {...register('slug')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink read-only:bg-surface-sunken read-only:text-ink-muted"
            />
            {slugLocked && (
              <p className="text-xs text-ink-muted">
                The slug cannot change once this has been published.
              </p>
            )}
            {errors.slug && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.slug.message}
              </p>
            )}
          </div>

          {kind === 'Banner' && (
            <>
              <div className="flex flex-col gap-1.5">
                <label htmlFor="content-placement" className="text-sm font-semibold text-ink">
                  Placement
                </label>
                <select
                  id="content-placement"
                  {...register('placement')}
                  className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
                >
                  <option value="">None</option>
                  {BANNER_PLACEMENTS.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
              </div>
              <div className="flex flex-col gap-1.5">
                <label htmlFor="content-link" className="text-sm font-semibold text-ink">
                  Link URL
                </label>
                <input
                  id="content-link"
                  {...register('linkUrl')}
                  className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
                />
              </div>
              <div className="flex flex-wrap gap-3">
                <div className="flex flex-col gap-1.5">
                  <label htmlFor="content-visible-from" className="text-sm font-semibold text-ink">
                    Visible from
                  </label>
                  <input
                    id="content-visible-from"
                    type="datetime-local"
                    {...register('visibleFrom')}
                    className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label htmlFor="content-visible-to" className="text-sm font-semibold text-ink">
                    Visible to
                  </label>
                  <input
                    id="content-visible-to"
                    type="datetime-local"
                    {...register('visibleTo')}
                    className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
                  />
                </div>
              </div>
            </>
          )}

          {kind === 'Faq' && (
            <div className="flex flex-col gap-1.5">
              <label htmlFor="content-sort-order" className="text-sm font-semibold text-ink">
                Sort order
              </label>
              <input
                id="content-sort-order"
                type="number"
                {...register('sortOrder', { valueAsNumber: true })}
                className="w-28 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
              />
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <label htmlFor="content-body" className="text-sm font-semibold text-ink">
              Body
            </label>
            <textarea
              id="content-body"
              rows={12}
              {...register('draftBody')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 font-mono text-sm text-ink"
            />
            <details className="text-xs text-ink-muted">
              <summary className="cursor-pointer font-semibold">What markup is supported?</summary>
              <pre className="mt-1 whitespace-pre-wrap">{MARKUP_HELP}</pre>
            </details>
          </div>

          <div className="flex flex-wrap gap-3">
            <Button
              type="submit"
              disabled={isSubmitting || isPending}
              aria-busy={isSubmitting || isPending}
            >
              {isSubmitting || isPending ? 'Saving…' : isCreate ? 'Save draft' : 'Save draft'}
            </Button>
            {!isCreate && item.status !== 'Published' && (
              <Button type="button" variant="secondary" onClick={() => setPublishConfirmOpen(true)}>
                Publish
              </Button>
            )}
            {!isCreate && item.status === 'Published' && (
              <Button
                type="button"
                variant="secondary"
                onClick={() => setUnpublishConfirmOpen(true)}
              >
                Unpublish
              </Button>
            )}
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold text-ink">Preview</h2>
          <div className="prose max-w-none rounded-[var(--radius-md)] border border-border bg-surface p-4">
            <h1>{title || 'Untitled'}</h1>
            <LimitedMarkup body={body} />
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={publishConfirmOpen}
        onOpenChange={setPublishConfirmOpen}
        title="Publish this content?"
        consequence={
          kind === 'Page'
            ? `This replaces what the public sees at /pages/${item?.slug ?? ''}.`
            : kind === 'Faq'
              ? 'This FAQ will appear on the public FAQ list.'
              : 'This banner will appear wherever its placement is shown.'
        }
        confirmLabel="Publish"
        onConfirm={handlePublish}
      />
      <ConfirmDialog
        open={unpublishConfirmOpen}
        onOpenChange={setUnpublishConfirmOpen}
        title="Unpublish this content?"
        consequence={
          kind === 'Page'
            ? `This takes /pages/${item?.slug ?? ''} offline. The public will see "Page not found" until it is published again.`
            : kind === 'Faq'
              ? 'This FAQ will be removed from the public FAQ list.'
              : 'This banner will stop appearing wherever its placement is shown.'
        }
        confirmLabel="Unpublish"
        variant="destructive"
        onConfirm={handleUnpublish}
      />
    </form>
  );
}
