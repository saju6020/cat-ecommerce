import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { RatingText } from '@/components/product/StatusText';
import { formatShortInstant } from '@/lib/format/shop';
import type { PagedResult } from '@/lib/api/types';
import type { ProductReview, RatingSummary } from '@/lib/api/shop-catalogue';
import type { ReviewEligibility } from '@/lib/api/shop-customer';

export interface ReviewsSectionProps {
  slug: string;
  rating: RatingSummary;
  reviews: PagedResult<ProductReview> | undefined;
  isLoading?: boolean;
  isError?: boolean;
  onRetry?: () => void;
  onPage: (pageNumber: number) => void;
  /** `guest`, or the signed-in customer's eligibility (`undefined` while it loads). */
  audience: 'guest' | 'customer';
  eligibility?: ReviewEligibility;
  signInHref: string;
}

function ReviewActions({
  slug,
  audience,
  eligibility,
  signInHref,
}: Pick<ReviewsSectionProps, 'slug' | 'audience' | 'eligibility' | 'signInHref'>) {
  if (audience === 'guest') {
    return (
      <Link href={signInHref} className="text-sm font-semibold text-accent-ink">
        Sign in to review products you have bought
      </Link>
    );
  }
  if (!eligibility) return null;
  if (eligibility.canReview) {
    return (
      <Link
        href={`/portal/reviews/new?product=${encodeURIComponent(slug)}`}
        className={buttonVariants({ variant: 'primary' })}
      >
        Write a review
      </Link>
    );
  }
  if (eligibility.reason === 'AlreadyReviewed') {
    return (
      <Link
        href={`/portal/reviews/new?product=${encodeURIComponent(slug)}${eligibility.existingReviewId ? `&reviewId=${eligibility.existingReviewId}` : ''}`}
        className={buttonVariants({ variant: 'secondary' })}
      >
        Edit your review
      </Link>
    );
  }
  return (
    <p className="text-sm text-ink-muted">Only customers who bought this product can review it.</p>
  );
}

/**
 * HO-SHP-002 §11.3 `ReviewsSection` — US-SHP-09. The rating summary, the paged PUBLISHED reviews
 * and the right call to action for this visitor. Review title and body are customer-written PLAIN
 * TEXT: rendered as text, never HTML or markup (HO-SHP-001 backlog SHP row 22, U-B12).
 */
export function ReviewsSection(props: ReviewsSectionProps) {
  const { rating, reviews, isLoading, isError, onRetry, onPage } = props;
  const pageCount = reviews ? Math.max(1, Math.ceil(reviews.totalCount / reviews.pageSize)) : 1;
  return (
    <section
      id="reviews"
      aria-labelledby="reviews-heading"
      className="flex scroll-mt-4 flex-col gap-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h2 id="reviews-heading" className="font-display text-xl font-semibold text-ink">
            Reviews
          </h2>
          <RatingText rating={rating} />
        </div>
        <ReviewActions {...props} />
      </div>

      {isLoading && <LoadingState rows={3} label="Loading reviews…" />}
      {isError && <ErrorState onRetry={onRetry} />}
      {reviews && reviews.items.length === 0 && (
        <p className="text-sm text-ink-muted">
          No reviews yet. Reviews appear after they are checked.
        </p>
      )}
      {reviews && reviews.items.length > 0 && (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
          {reviews.items.map((review) => (
            <li key={review.reviewId}>
              <article className="flex flex-col gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface p-4">
                <p className="text-sm font-semibold text-ink">
                  {review.rating} out of 5{review.title ? ` — ${review.title}` : ''}
                </p>
                <p className="whitespace-pre-line text-sm text-ink">{review.body}</p>
                <p className="text-xs text-ink-muted">
                  {review.authorDisplayName}
                  {review.verifiedPurchase ? ' · verified buyer' : ''} · {review.skuLabel} ·{' '}
                  {formatShortInstant(review.submittedOn)}
                </p>
              </article>
            </li>
          ))}
        </ul>
      )}
      {reviews && pageCount > 1 && (
        <nav aria-label="Review pages" className="flex items-center gap-3">
          <Button
            variant="secondary"
            size="sm"
            disabled={reviews.pageNumber <= 1}
            onClick={() => onPage(reviews.pageNumber - 1)}
          >
            Previous reviews
          </Button>
          <span className="text-sm text-ink-muted">
            Page {reviews.pageNumber} of {pageCount}
          </span>
          <Button
            variant="secondary"
            size="sm"
            disabled={reviews.pageNumber >= pageCount}
            onClick={() => onPage(reviews.pageNumber + 1)}
          >
            More reviews
          </Button>
        </nav>
      )}
    </section>
  );
}
