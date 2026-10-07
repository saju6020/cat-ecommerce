'use client';

import { use } from 'react';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { usePublishedContent } from '@/lib/hooks/usePublicContent';
import { ApiNotFoundError } from '@/lib/api/client';

/** `/pages/[slug]` — HO-ADM-002 §8.1/§11.6, US-ADM-07. `GET /api/content/page/{slug}` — published
 *  only; a `404` shows a plain not-found page, never an error banner. */
export default function PublicPageRenderer({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = use(params);
  const { data, isLoading, isError, error, refetch } = usePublishedContent('Page', slug);

  if (isLoading) return <LoadingState rows={6} />;
  if (error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        headingLevel={1}
        title="Page not found"
        description="This page does not exist or is not published."
      />
    );
  }
  if (isError || !data) {
    return <ErrorState onRetry={() => refetch()} />;
  }

  return (
    <article className="mx-auto max-w-2xl px-6 py-10">
      <h1 className="font-display text-2xl font-semibold text-ink">{data.title}</h1>
      <LimitedMarkup body={data.body} className="prose mt-6 max-w-none" />
    </article>
  );
}
