'use client';

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ReviewForm } from '@/components/reviews/ReviewForm';
import { useProductDetail } from '@/lib/hooks/useShopCatalogue';
import {
  useEditReview,
  useMyReviews,
  useReviewEligibility,
  useSubmitReview,
} from '@/lib/hooks/useShopCustomer';

function NewReviewContent() {
  const router = useRouter();
  const params = useSearchParams();
  const slug = params.get('product') ?? '';
  const reviewId = params.get('reviewId') ?? '';
  const done = () => router.push('/portal/reviews?submitted=1');

  const mine = useMyReviews(!!reviewId);
  const editing = mine.data?.find((r) => r.reviewId === reviewId);
  const detail = useProductDetail(reviewId ? '' : slug);
  const eligibility = useReviewEligibility(slug, !!slug && !reviewId);
  const submit = useSubmitReview();
  const edit = useEditReview(reviewId);

  if (reviewId) {
    if (mine.isPending) return <LoadingState rows={4} />;
    if (!editing)
      return (
        <ErrorState
          title="We couldn't find that review."
          description="It may have been withdrawn."
        />
      );
    return (
      <ReviewForm
        isEdit
        productName={editing.productName}
        purchasedSkus={[{ skuId: 'edit', label: editing.skuLabel }]}
        initial={{
          skuId: 'edit',
          rating: editing.rating,
          title: editing.title,
          body: editing.body,
        }}
        onSave={async (values) => {
          await edit.mutateAsync({ rating: values.rating, title: values.title, body: values.body });
          done();
        }}
      />
    );
  }

  if (!slug)
    return (
      <ErrorState
        title="Choose a product to review."
        description="Open a product you bought and select Write a review."
      />
    );
  if (detail.isPending || eligibility.isPending) return <LoadingState rows={4} />;
  if (detail.isError || eligibility.isError || !detail.data || !eligibility.data) {
    return (
      <ErrorState
        onRetry={() => {
          void detail.refetch();
          void eligibility.refetch();
        }}
      />
    );
  }
  if (!eligibility.data.canReview) {
    return (
      <ErrorState
        title="You can't review this product right now."
        description={
          eligibility.data.reason === 'AlreadyReviewed'
            ? 'You have already reviewed this product. You can edit that review from My reviews.'
            : 'Only customers who bought this product can review it.'
        }
      />
    );
  }
  const product = detail.data;
  return (
    <ReviewForm
      productName={product.name}
      purchasedSkus={eligibility.data.purchasedSkus}
      onSave={async (values) => {
        await submit.mutateAsync({ productId: product.productId, ...values });
        done();
      }}
    />
  );
}

/** `/portal/reviews/new?product={slug}[&reviewId=]` — US-SHP-09: write or edit a review. */
export default function NewReviewPage() {
  return (
    <Suspense fallback={<LoadingState rows={4} />}>
      <NewReviewContent />
    </Suspense>
  );
}
