import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { getStaffProduct, getSkuPrices, publishReview } from '@/lib/api/staff-shop';
import { getFoodRecommendations, addWishlistItem, withdrawReview } from '@/lib/api/shop-customer';

/** S10 — a route id with `/` or `?` must stay one percent-encoded path segment. */
const TRICKY = '../x?y=1';
const ENCODED = '..%2Fx%3Fy%3D1';

function capture(method: 'get' | 'put' | 'post' | 'delete', pattern: string) {
  const seen: string[] = [];
  server.use(
    http[method](pattern, ({ request }) => {
      seen.push(new URL(request.url).pathname);
      return HttpResponse.json({});
    }),
  );
  return seen;
}

describe('shop API paths encode their ids — S10', () => {
  it('staff product', async () => {
    const seen = capture('get', '*/api/staff/shop/products/:productId');
    await getStaffProduct(TRICKY);
    expect(seen).toEqual([`/api/staff/shop/products/${ENCODED}`]);
  });

  it('staff sku prices', async () => {
    const seen = capture('get', '*/api/staff/shop/skus/:skuId/prices');
    await getSkuPrices(TRICKY);
    expect(seen).toEqual([`/api/staff/shop/skus/${ENCODED}/prices`]);
  });

  it('staff review action', async () => {
    const seen = capture('post', '*/api/staff/shop/reviews/:reviewId/publish');
    await publishReview(TRICKY);
    expect(seen).toEqual([`/api/staff/shop/reviews/${ENCODED}/publish`]);
  });

  it('customer food recommendations', async () => {
    const seen = capture('get', '*/api/customers/me/cats/:catId/food-recommendations');
    await getFoodRecommendations(TRICKY);
    expect(seen).toEqual([`/api/customers/me/cats/${ENCODED}/food-recommendations`]);
  });

  it('customer wishlist item and review', async () => {
    const wish = capture('put', '*/api/customers/me/wishlist/items/:skuId');
    await addWishlistItem(TRICKY);
    expect(wish).toEqual([`/api/customers/me/wishlist/items/${ENCODED}`]);
    const review = capture('delete', '*/api/customers/me/reviews/:reviewId');
    await withdrawReview(TRICKY);
    expect(review).toEqual([`/api/customers/me/reviews/${ENCODED}`]);
  });
});
