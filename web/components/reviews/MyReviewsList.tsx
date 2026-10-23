'use client';

import * as React from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { formatShortInstant } from '@/lib/format/shop';
import type { MyReview, MyReviewStatus } from '@/lib/api/shop-customer';

const STATUS_TEXT: Record<
  MyReviewStatus,
  { text: string; variant: 'neutral' | 'success' | 'warning' | 'danger' }
> = {
  PendingModeration: { text: 'Awaiting approval', variant: 'warning' },
  Published: { text: 'Published', variant: 'success' },
  Rejected: { text: 'Not published', variant: 'danger' },
  Hidden: { text: 'Hidden', variant: 'neutral' },
};

export interface MyReviewsListProps {
  reviews: MyReview[];
  onWithdraw: (reviewId: string) => Promise<unknown>;
}

/** HO-SHP-002 §11.5 `MyReviewsList` — each review with its status in TEXT, **Edit** and **Withdraw**
 *  (confirmed). Title and body are plain text. */
export function MyReviewsList({ reviews, onWithdraw }: MyReviewsListProps) {
  const [target, setTarget] = React.useState<MyReview | null>(null);
  if (reviews.length === 0) {
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="You haven't written any reviews yet"
        description="After you buy something, you can review it from its product page."
        action={
          <Link href="/shop/products" className={buttonVariants({ variant: 'secondary' })}>
            Browse the shop
          </Link>
        }
      />
    );
  }
  return (
    <>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {reviews.map((review) => {
          const status = STATUS_TEXT[review.status];
          return (
            <li key={review.reviewId}>
              <article className="flex flex-col gap-2 rounded-card border border-border bg-surface p-5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h2 className="text-base font-semibold text-ink">{review.productName}</h2>
                  <Badge variant={status.variant}>{status.text}</Badge>
                </div>
                <p className="text-sm font-semibold text-ink">
                  {review.rating} out of 5{review.title ? ` — ${review.title}` : ''}
                </p>
                <p className="whitespace-pre-line text-sm text-ink">{review.body}</p>
                <p className="text-xs text-ink-muted">
                  {review.skuLabel} · {formatShortInstant(review.submittedOn)}
                </p>
                <div className="flex gap-2">
                  <Link
                    href={`/portal/reviews/new?reviewId=${review.reviewId}`}
                    className={buttonVariants({ variant: 'secondary', size: 'sm' })}
                  >
                    Edit
                  </Link>
                  <Button variant="pillSecondary" size="pillMd" onClick={() => setTarget(review)}>
                    Withdraw
                  </Button>
                </div>
              </article>
            </li>
          );
        })}
      </ul>
      <ConfirmDialog
        open={target !== null}
        onOpenChange={(open) => !open && setTarget(null)}
        title="Withdraw this review?"
        consequence={`Your review of ${target?.productName ?? ''} will be removed.`}
        confirmLabel="Withdraw"
        variant="destructive"
        onConfirm={async () => {
          if (target) await onWithdraw(target.reviewId);
          setTarget(null);
        }}
      />
    </>
  );
}
