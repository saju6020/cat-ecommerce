import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReviewsSection } from '@/components/reviews/ReviewsSection';
import { RecommendationBlocks } from '@/components/recommendations/RecommendationBlocks';
import { StockAlertButton } from '@/components/wishlist/StockAlertButton';
import type { ProductReview, Recommendations } from '@/lib/api/shop-catalogue';
import type { ReviewEligibility } from '@/lib/api/shop-customer';
import { makeItem } from '../catalogue/catalogue-fixtures';

const REVIEW: ProductReview = {
  reviewId: 'r1',
  rating: 5,
  title: 'Great <b>food</b>',
  body: 'Loved it **really** <script>alert(1)</script>',
  authorDisplayName: 'Rahim K.',
  skuLabel: '2 kg',
  submittedOn: '2026-10-03T09:00:00Z',
  verifiedPurchase: true,
};

function section(
  audience: 'guest' | 'customer',
  eligibility?: ReviewEligibility,
  reviews: ProductReview[] = [REVIEW],
) {
  return render(
    <ReviewsSection
      slug="royal-canin-kitten"
      rating={{ average: 4.5, count: 23 }}
      reviews={{ items: reviews, pageNumber: 1, pageSize: 10, totalCount: reviews.length }}
      onPage={() => {}}
      audience={audience}
      eligibility={eligibility}
      signInHref="/sign-in?next=%2Fshop"
    />,
  );
}

describe('ReviewsSection — HO-SHP-002 §11.3, US-SHP-09', () => {
  it('U-B8: canReview offers Write a review', () => {
    section('customer', {
      canReview: true,
      reason: null,
      purchasedSkus: [],
      existingReviewId: null,
    });
    expect(screen.getByRole('link', { name: 'Write a review' })).toHaveAttribute(
      'href',
      '/portal/reviews/new?product=royal-canin-kitten',
    );
  });

  it('U-B8: AlreadyReviewed offers Edit your review for that review', () => {
    section('customer', {
      canReview: false,
      reason: 'AlreadyReviewed',
      purchasedSkus: [],
      existingReviewId: 'rv-9',
    });
    expect(screen.getByRole('link', { name: 'Edit your review' })).toHaveAttribute(
      'href',
      '/portal/reviews/new?product=royal-canin-kitten&reviewId=rv-9',
    );
    expect(screen.queryByRole('link', { name: 'Write a review' })).not.toBeInTheDocument();
  });

  it('U-B8: NotPurchased says why, in plain language', () => {
    section('customer', {
      canReview: false,
      reason: 'NotPurchased',
      purchasedSkus: [],
      existingReviewId: null,
    });
    expect(
      screen.getByText('Only customers who bought this product can review it.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /review/i })).not.toBeInTheDocument();
  });

  it('a guest is invited to sign in', () => {
    section('guest');
    expect(screen.getByRole('link', { name: /Sign in to review/ })).toHaveAttribute(
      'href',
      '/sign-in?next=%2Fshop',
    );
  });

  it('U-B12: review title and body render literally as text (no HTML, no markdown)', () => {
    const { container } = section('guest');
    expect(screen.getByText(/Great <b>food<\/b>/)).toBeInTheDocument();
    expect(
      screen.getByText(/Loved it \*\*really\*\* <script>alert\(1\)<\/script>/),
    ).toBeInTheDocument();
    expect(container.querySelector('b, strong, script')).toBeNull();
  });

  it('shows rating as text, the author line and verified-buyer wording', () => {
    section('guest');
    expect(screen.getByText('4.5 out of 5 (23 reviews)')).toBeInTheDocument();
    expect(screen.getByText('5 out of 5 — Great <b>food</b>')).toBeInTheDocument();
    expect(screen.getByText('Rahim K. · verified buyer · 2 kg · 3 Oct 2026')).toBeInTheDocument();
  });

  it('says so when there are no reviews yet', () => {
    section('guest', undefined, []);
    expect(
      screen.getByText(/No reviews yet\. Reviews appear after they are checked/),
    ).toBeInTheDocument();
  });
});

describe('RecommendationBlocks — US-SHP-08', () => {
  const recs = (overrides: Partial<Recommendations> = {}): Recommendations => ({
    frequentlyBoughtTogether: [makeItem({ productId: 'a', name: 'Bundle Mate' })],
    upSell: [],
    lifeStage: {
      basis: 'Generic',
      lifeStages: ['Kitten'],
      items: [makeItem({ productId: 'b', name: 'Kitten Pick' })],
    },
    ...overrides,
  });

  it('U-B9: an empty block is not rendered; the life-stage heading names the audience', () => {
    render(<RecommendationBlocks recommendations={recs()} />);
    expect(screen.getByRole('heading', { name: 'Frequently bought together' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'You might also like' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Recommended for kittens' })).toBeInTheDocument();
  });

  it('U-B9: the heading reads "for your cats" when basis is YourCats', () => {
    render(
      <RecommendationBlocks
        recommendations={recs({
          lifeStage: {
            basis: 'YourCats',
            lifeStages: ['Kitten'],
            items: [makeItem({ productId: 'b' })],
          },
        })}
      />,
    );
    expect(screen.getByRole('heading', { name: 'Recommended for your cats' })).toBeInTheDocument();
  });

  it('renders nothing at all when every block is empty', () => {
    const { container } = render(
      <RecommendationBlocks
        recommendations={{
          frequentlyBoughtTogether: [],
          upSell: [],
          lifeStage: { basis: 'Generic', lifeStages: [], items: [] },
        }}
      />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});

describe('StockAlertButton — ADR-FE-038, US-SHP-13', () => {
  it('U-B10: willNotify false says nothing will reach the customer and links to notification settings', () => {
    render(
      <StockAlertButton
        requested
        deliverability={{ willNotify: false, channels: [], inboxCopy: false }}
        onRequest={() => {}}
        onCancel={() => {}}
      />,
    );
    const status = screen.getByRole('status');
    expect(status).toHaveTextContent(
      "You haven't agreed to receive offers and updates, so we can't tell you when this is back.",
    );
    expect(within(status).getByRole('link', { name: 'notification settings' })).toHaveAttribute(
      'href',
      '/portal/account/notifications',
    );
    expect(screen.queryByText(/We.ll send one message/)).not.toBeInTheDocument();
  });

  it('willNotify true names the channels; Cancel alert is offered', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    render(
      <StockAlertButton
        requested
        deliverability={{ willNotify: true, channels: ['InApp', 'Email'], inboxCopy: true }}
        onRequest={() => {}}
        onCancel={onCancel}
      />,
    );
    expect(screen.getByRole('status')).toHaveTextContent(
      "We'll send one message by in-app notification and email.",
    );
    await user.click(screen.getByRole('button', { name: 'Cancel alert' }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('before any request it offers the button', async () => {
    const user = userEvent.setup();
    const onRequest = vi.fn();
    render(<StockAlertButton requested={false} onRequest={onRequest} onCancel={() => {}} />);
    await user.click(screen.getByRole('button', { name: /Tell me when it.s back/ }));
    expect(onRequest).toHaveBeenCalled();
  });
});
