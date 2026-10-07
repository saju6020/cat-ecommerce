'use client';

import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { MyReviewsList } from '@/components/reviews/MyReviewsList';
import { useMyReviews, useWithdrawReview } from '@/lib/hooks/useShopCustomer';

function MyReviewsContent() {
  const reviews = useMyReviews();
  const withdraw = useWithdrawReview();
  const submitted = useSearchParams().get('submitted') === '1';
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">My reviews</h1>
      {submitted && (
        <p
          role="status"
          className="rounded-[var(--radius-sm)] bg-accent-soft p-3 text-sm font-semibold text-accent-ink"
        >
          Thanks — your review will appear once it&apos;s approved.
        </p>
      )}
      {reviews.isPending && <LoadingState rows={3} label="Loading your reviews…" />}
      {reviews.isError && <ErrorState onRetry={() => reviews.refetch()} />}
      {reviews.data && (
        <MyReviewsList reviews={reviews.data} onWithdraw={(id) => withdraw.mutateAsync(id)} />
      )}
    </div>
  );
}

/** `/portal/reviews` — US-SHP-09: my reviews with their status. */
export default function MyReviewsPage() {
  return (
    <Suspense fallback={<LoadingState rows={3} />}>
      <MyReviewsContent />
    </Suspense>
  );
}
