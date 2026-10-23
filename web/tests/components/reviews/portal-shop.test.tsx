import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReviewForm } from '@/components/reviews/ReviewForm';
import { MyReviewsList } from '@/components/reviews/MyReviewsList';
import { StockAlertList, WishlistList } from '@/components/wishlist/WishlistList';
import { FoodSuggestions } from '@/components/recommendations/FoodSuggestions';
import { ApiValidationError } from '@/lib/api/client';
import type {
  FoodRecommendations,
  MyReview,
  StockAlert,
  WishlistItem,
} from '@/lib/api/shop-customer';
import { makeItem } from '../catalogue/catalogue-fixtures';
import { renderWithClient } from '../product/test-utils';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/portal/reviews/new',
  useSearchParams: () => new URLSearchParams(),
}));

const SKUS = [{ skuId: 's1', label: '2 kg' }];

function fillValid(user: ReturnType<typeof userEvent.setup>) {
  return (async () => {
    await user.click(screen.getByRole('radio', { name: '4 out of 5' }));
    await user.type(screen.getByLabelText(/Your review/), 'Lovely food.');
  })();
}

describe('FoodSuggestions — HO-SHP-002 §11.5, US-SHP-11', () => {
  const data: FoodRecommendations = {
    cat: { catId: 'c1', name: 'Mitu', lifeStage: 'Adult', lifeStageIsEstimated: true },
    items: [
      {
        item: makeItem({ productId: 'a', name: 'Pick A' }),
        reasons: [
          { code: 'LifeStageMatch', values: { lifeStage: 'Adult', about: 'true' } },
          { code: 'Sterilised', values: {} },
          { code: 'WeightManagement', values: { weightKg: '6.80' } },
          { code: 'ConditionMatch', values: { focus: 'Urinary' } },
        ],
      },
    ],
    excludedForAllergy: [{ allergen: 'Chicken', count: 3 }],
    hints: ['AddDateOfBirth'],
  };

  it('U-E1: each reason as text, the allergy exclusion sentence, and the date-of-birth hint link', () => {
    renderWithClient(<FoodSuggestions data={data} />);
    expect(screen.getByText('Suits about adult cats')).toBeInTheDocument();
    expect(screen.getByText('For neutered cats')).toBeInTheDocument();
    expect(screen.getByText('Weight-management recipe — Mitu weighs 6.8 kg')).toBeInTheDocument();
    expect(screen.getByText("May suit Mitu's recorded urinary condition")).toBeInTheDocument();
    expect(
      screen.getByText('We left out 3 products containing chicken (recorded allergy).'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Go to Mitu.s profile/ })).toHaveAttribute(
      'href',
      '/portal/cats/c1',
    );
    expect(
      screen.getByText('General guidance — ask our vets for advice for your cat.'),
    ).toBeInTheDocument();
  });

  it('says so when there is nothing to suggest', () => {
    renderWithClient(
      <FoodSuggestions data={{ ...data, items: [], excludedForAllergy: [], hints: [] }} />,
    );
    expect(screen.getByText('No suggestions right now')).toBeInTheDocument();
  });
});

describe('ReviewForm — US-SHP-09', () => {
  it('U-E2: a server ReviewNotPermitted shows in plain language and keeps the values', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'ProductId',
            errorCode: 'ReviewNotPermitted',
            errorMessage: 'Only customers who bought this product can review it.',
            resourceName: 'ProductReview',
          },
        ],
      }),
    );
    render(<ReviewForm productName="Royal Canin Kitten" purchasedSkus={SKUS} onSave={onSave} />);
    await fillValid(user);
    await user.click(screen.getByRole('button', { name: 'Send review' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Only customers who bought this product can review it.',
    );
    expect(screen.getByLabelText(/Your review/)).toHaveValue('Lovely food.');
    expect(screen.getByRole('radio', { name: '4 out of 5' })).toBeChecked();
    // U-X1: the error code is never in the DOM.
    expect(document.body.textContent).not.toContain('ReviewNotPermitted');
  });

  it('U-X1: a field error is mapped to its field (no code shown)', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'Body',
            errorCode: 'InvalidReviewText',
            errorMessage: 'The review text is not allowed.',
            resourceName: 'ProductReview',
          },
        ],
      }),
    );
    render(<ReviewForm productName="X" purchasedSkus={SKUS} onSave={onSave} />);
    await fillValid(user);
    await user.click(screen.getByRole('button', { name: 'Send review' }));
    const field = await screen.findByLabelText(/Your review/);
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(await screen.findByText('The review text is not allowed.')).toBeInTheDocument();
    expect(document.body.textContent).not.toContain('InvalidReviewText');
  });

  it('requires a rating and review text, and counts characters (announced near the limit)', async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    render(<ReviewForm productName="X" purchasedSkus={SKUS} onSave={onSave} />);
    expect(screen.getByText('2000 characters left')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send review' }));
    expect(await screen.findByText('Choose a rating from 1 to 5.')).toBeInTheDocument();
    expect(screen.getByText('Write a few words about the product.')).toBeInTheDocument();
    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText('Reviews are checked before they appear.')).toBeInTheDocument();
  });

  it('U-X2: a dirty review form asks before a link navigates away', async () => {
    const user = userEvent.setup();
    render(
      <>
        <a href="/shop">Leave</a>
        <ReviewForm productName="X" purchasedSkus={SKUS} onSave={vi.fn()} />
      </>,
    );
    await user.type(screen.getByLabelText(/Your review/), 'half written');
    await user.click(screen.getByRole('link', { name: 'Leave' }));
    expect(await screen.findByRole('dialog', { name: 'Leave this page?' })).toBeInTheDocument();
  });
});

const REVIEWS: MyReview[] = (['PendingModeration', 'Published', 'Rejected', 'Hidden'] as const).map(
  (status, i) => ({
    reviewId: `r${i}`,
    productId: 'p',
    productName: `Product ${i}`,
    skuLabel: '2 kg',
    rating: 4,
    title: i === 0 ? '<b>Nice</b>' : null,
    body: 'Body **text**',
    status,
    submittedOn: '2026-10-02T09:00:00Z',
  }),
);

describe('MyReviewsList / WishlistList / StockAlertList', () => {
  it('shows each review status in text, edit links, and plain-text content', async () => {
    const user = userEvent.setup();
    const onWithdraw = vi.fn().mockResolvedValue(undefined);
    const { container } = render(<MyReviewsList reviews={REVIEWS} onWithdraw={onWithdraw} />);
    for (const text of ['Awaiting approval', 'Published', 'Not published', 'Hidden']) {
      expect(screen.getByText(text)).toBeInTheDocument();
    }
    expect(screen.getAllByRole('link', { name: 'Edit' })[0]).toHaveAttribute(
      'href',
      '/portal/reviews/new?reviewId=r0',
    );
    expect(container.querySelector('b, strong')).toBeNull();
    await user.click(screen.getAllByRole('button', { name: 'Withdraw' })[0]!);
    const dialog = await screen.findByRole('dialog', { name: 'Withdraw this review?' });
    await user.click(within(dialog).getByRole('button', { name: 'Withdraw' }));
    expect(onWithdraw).toHaveBeenCalledWith('r0');
  });

  it('U-E3: wishlist shows No longer sold when onSale is false; alerts show status text', async () => {
    const user = userEvent.setup();
    const item = (over: Partial<WishlistItem>): WishlistItem => ({
      skuId: 's',
      productId: 'p',
      slug: 'slug',
      productName: 'Thing',
      skuLabel: '1 kg',
      price: { amount: '100.00', currencyCode: 'BDT' },
      availability: 'InStock',
      onSale: true,
      addedOn: '2026-10-01T00:00:00Z',
      primaryImage: null,
      ...over,
    });
    const onRemove = vi.fn();
    render(
      <WishlistList
        items={[item({ skuId: 'a' }), item({ skuId: 'b', productName: 'Gone', onSale: false })]}
        onRemove={onRemove}
      />,
    );
    expect(screen.getByText('No longer sold')).toBeInTheDocument();
    expect(screen.getAllByText('In stock')).toHaveLength(1);
    expect(screen.getAllByRole('link', { name: 'Thing' })[0]).toHaveAttribute(
      'href',
      '/shop/products/slug?sku=a',
    );
    await user.click(screen.getByRole('button', { name: /Remove Gone/ }));
    expect(onRemove).toHaveBeenCalledWith('b');

    const alert = (over: Partial<StockAlert>): StockAlert => ({
      stockAlertId: 'x',
      skuId: 's',
      slug: 'slug',
      productName: 'Thing',
      skuLabel: '1 kg',
      status: 'Pending',
      requestedOn: '2026-10-01T00:00:00Z',
      notifiedOn: null,
      ...over,
    });
    const onCancel = vi.fn();
    render(
      <StockAlertList
        alerts={[
          alert({ stockAlertId: '1' }),
          alert({ stockAlertId: '2', status: 'Notified', notifiedOn: '2026-10-03T08:00:00Z' }),
          alert({ stockAlertId: '3', status: 'Closed' }),
        ]}
        onCancel={onCancel}
      />,
    );
    expect(screen.getByText('Waiting')).toBeInTheDocument();
    expect(screen.getByText('Notified on 3 Oct 2026')).toBeInTheDocument();
    expect(screen.getByText('Closed — no longer sold')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Cancel' })).toHaveLength(1);
  });
});
