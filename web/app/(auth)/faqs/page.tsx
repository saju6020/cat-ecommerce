'use client';

import { FaqList } from '@/components/content/FaqList';
import { usePublishedFaqs } from '@/lib/hooks/usePublicContent';

/** `/faqs` — HO-ADM-002 §8.1/§11.6, US-ADM-07. `GET /api/content/faqs` — published, sorted. */
export default function FaqsPage() {
  const { data, isLoading, isError, refetch } = usePublishedFaqs();

  return (
    <div className="mx-auto max-w-2xl px-6 py-10">
      <h1 className="mb-6 font-display text-2xl font-semibold text-ink">
        Frequently asked questions
      </h1>
      <FaqList faqs={data} isLoading={isLoading} isError={isError} onRetry={() => refetch()} />
    </div>
  );
}
