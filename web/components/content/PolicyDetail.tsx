'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { LimitedMarkup } from './LimitedMarkup';
import { applyValidationErrors, describeApiError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { formatInstantDate } from '@/lib/format/time';
import type { PolicyDocument, PublishPolicyVersionRequest } from '@/lib/api/admin-content';

const schema = z.object({
  body: z.string().trim().min(1, 'Enter the policy text.').max(50_000),
  changeSummary: z.string().trim().min(1, 'Summarise what changed.').max(500),
});
type FormValues = z.infer<typeof schema>;
const FIELD_MAP = { Body: 'body', ChangeSummary: 'changeSummary' } as const;

/**
 * `components/content/PolicyDetail.tsx` — HO-ADM-002 §11.6, US-ADM-07, ADR-FE-019. Version list
 * is read-only (append-only, never edited); "Publish new version" is shown only when `canManage`
 * is true (Administrator only, owner decision 2026-09-26) — presentation only, the server refuses
 * the call with `403` for anyone else regardless.
 */
export interface PolicyDetailProps {
  policy: PolicyDocument;
  canManage: boolean;
  onPublishVersion: (request: PublishPolicyVersionRequest) => Promise<unknown>;
  isPending: boolean;
}

export function PolicyDetail({
  policy,
  canManage,
  onPublishVersion,
  isPending,
}: PolicyDetailProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    getValues,
    setError,
    reset,
    formState: { errors, isDirty, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { body: '', changeSummary: '' },
  });

  function onSubmit() {
    setConfirmOpen(true);
  }

  async function handleConfirmPublish() {
    setPublishError(null);
    try {
      await onPublishVersion(getValues());
      setConfirmOpen(false);
      reset({ body: '', changeSummary: '' });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP });
        setConfirmOpen(false);
      } else {
        setPublishError(describeApiError(error));
      }
    }
  }

  const sortedVersions = [...policy.versions].sort((a, b) => b.versionNumber - a.versionNumber);

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="font-display text-xl font-semibold text-ink">{policy.title}</h1>
        <p className="text-sm text-ink-muted">
          {policy.key} ·{' '}
          {policy.requiresAcceptance ? 'Requires customer acceptance' : 'No acceptance required'}
        </p>
      </div>

      <div className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold text-ink">Versions</h2>
        <ul className="flex flex-col gap-3">
          {sortedVersions.map((version) => (
            <li
              key={version.versionNumber}
              className="rounded-[var(--radius-md)] border border-border p-3"
            >
              <p className="text-sm font-semibold text-ink">
                Version {version.versionNumber} · published {formatInstantDate(version.publishedOn)}
                {version.versionNumber === policy.currentVersionNumber ? ' · Current' : ''}
              </p>
              <p className="text-sm text-ink-muted">{version.changeSummary}</p>
              <details className="mt-2 text-sm text-ink">
                <summary className="cursor-pointer font-semibold">Read text</summary>
                <LimitedMarkup body={version.body} className="mt-2" />
              </details>
            </li>
          ))}
          {sortedVersions.length === 0 && (
            <p className="text-sm text-ink-muted">No versions published yet.</p>
          )}
        </ul>
      </div>

      {canManage && (
        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-lg flex-col gap-3">
          <UnsavedChangesGuard when={isDirty} />
          <h2 className="text-sm font-semibold text-ink">Publish new version</h2>
          {publishError && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {publishError}
            </p>
          )}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="policy-body" className="text-sm font-semibold text-ink">
              Body
            </label>
            <textarea
              id="policy-body"
              rows={10}
              {...register('body')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 font-mono text-sm text-ink"
            />
            {errors.body && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.body.message}
              </p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="policy-change-summary" className="text-sm font-semibold text-ink">
              Change summary
            </label>
            <input
              id="policy-change-summary"
              {...register('changeSummary')}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
            {errors.changeSummary && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.changeSummary.message}
              </p>
            )}
          </div>
          <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting} className="w-fit">
            Publish new version
          </Button>
        </form>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Publish this new version?"
        consequence={
          policy.requiresAcceptance
            ? 'Signed-in customers will be asked to accept this new version before continuing to use their account.'
            : 'This becomes the current version of this policy.'
        }
        confirmLabel="Publish"
        onConfirm={handleConfirmPublish}
        pendingLabel={isPending ? 'Publishing…' : undefined}
      />
    </div>
  );
}
