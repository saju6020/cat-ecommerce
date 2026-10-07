'use client';

import * as React from 'react';
import { ReviewsSection } from '@/components/reviews/ReviewsSection';
import type { PagedResult } from '@/lib/api/types';
import type { ProductReview, RatingSummary } from '@/lib/api/shop-catalogue';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { useProductReviews } from '@/lib/hooks/useShopCatalogue';
import { useReviewEligibility } from '@/lib/hooks/useShopCustomer';
import { productHref } from '@/lib/shop/helpers';

/** Wires the reviews list (server-rendered first page, refreshed client side) and the visitor's
 *  review eligibility into `ReviewsSection`. A guest is never asked for eligibility. */
export function ProductReviews({
  slug,
  rating,
  initial,
  lang,
}: {
  slug: string;
  rating: RatingSummary;
  initial: PagedResult<ProductReview> | null;
  lang: string;
}) {
  const [page, setPage] = React.useState(1);
  const session = useCustomerSession();
  const signedIn = !!session.data;
  const reviews = useProductReviews(slug, page);
  const eligibility = useReviewEligibility(slug, signedIn);
  const signInHref = `/sign-in?next=${encodeURIComponent(productHref(slug, { lang: lang || undefined }))}`;

  return (
    <ReviewsSection
      slug={slug}
      rating={rating}
      reviews={reviews.data ?? (page === 1 ? (initial ?? undefined) : undefined)}
      isLoading={reviews.isPending && !initial}
      isError={reviews.isError && !initial}
      onRetry={() => reviews.refetch()}
      onPage={setPage}
      audience={signedIn ? 'customer' : 'guest'}
      eligibility={eligibility.data}
      signInHref={signInHref}
    />
  );
}
