'use client';

import { use } from 'react';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { usePublicPolicy } from '@/lib/hooks/usePublicContent';
import { ApiNotFoundError } from '@/lib/api/client';
import { formatInstantDate } from '@/lib/format/time';

/**
 * `/policies/[key]` — HO-ADM-002 §12.4 `GET /api/policies/{key}` (public), the readable page
 * `PolicyAcceptancePrompt`'s "read the full text" link opens in a new tab. Not itself named in
 * §8.1's route map (which lists only `pages/[slug]`/`faqs`); added because §12.4 makes the
 * endpoint public and the acceptance prompt needs somewhere real to send a customer to read the
 * text before accepting — see the dated Amended row this handover records for it.
 */
export default function PublicPolicyPage({ params }: { params: Promise<{ key: string }> }) {
  const { key } = use(params);
  const { data, isLoading, isError, error, refetch } = usePublicPolicy(key);

  if (isLoading) return <LoadingState rows={6} />;
  if (error instanceof ApiNotFoundError) {
    return <ErrorState title="Not found" description="This policy could not be found." />;
  }
  if (isError || !data) {
    return <ErrorState onRetry={() => refetch()} />;
  }

  return (
    <article className="mx-auto max-w-2xl px-6 py-10">
      <h1 className="font-display text-2xl font-semibold text-ink">{data.title}</h1>
      <p className="mt-1 text-sm text-ink-muted">
        Version {data.versionNumber} · published {formatInstantDate(data.publishedOn)}
      </p>
      <LimitedMarkup body={data.body} className="prose mt-6 max-w-none" />
    </article>
  );
}
