import { apiFetch } from './client';
import type { Money } from '@/lib/format/money';
import type { Availability, CatalogueItem, ProductImageRef } from './shop-catalogue';

/**
 * `lib/api/shop-customer.ts` — HO-SHP-002 §8.3. The signed-in customer's side of the shop
 * (HO-SHP-001 §12.4 "Customer"): purchasability, review eligibility, my reviews, wishlist, stock
 * alerts and food suggestions. Every route is `no-store`; another customer's id is a `404`.
 */

export type PurchaseBlockReason =
  'OutOfStock' | 'PrescriptionSignInRequired' | 'PrescriptionAuthorisationRequired' | 'NotOnSale';

export interface EligibleCat {
  catId: string;
  name: string;
  /** `yyyy-MM-dd` — the last day the authorisation is valid. */
  validTo: string;
}
export interface SkuPurchasability {
  skuId: string;
  purchasable: boolean;
  purchaseBlockReason: PurchaseBlockReason | null;
  eligibleCats: EligibleCat[];
}

export function getPurchasability(slug: string): Promise<{ skus: SkuPurchasability[] }> {
  return apiFetch(`/api/customers/me/shop/products/${encodeURIComponent(slug)}/purchasability`);
}

export interface ReviewEligibility {
  canReview: boolean;
  reason: 'NotPurchased' | 'AlreadyReviewed' | null;
  purchasedSkus: Array<{ skuId: string; label: string }>;
  existingReviewId: string | null;
}

export function getReviewEligibility(slug: string): Promise<ReviewEligibility> {
  return apiFetch(`/api/customers/me/shop/products/${encodeURIComponent(slug)}/review-eligibility`);
}

export type MyReviewStatus = 'PendingModeration' | 'Published' | 'Rejected' | 'Hidden';
export interface MyReview {
  reviewId: string;
  productId: string;
  productName: string;
  skuLabel: string;
  rating: number;
  title: string | null;
  body: string;
  status: MyReviewStatus;
  submittedOn: string;
}

export function getMyReviews(): Promise<MyReview[]> {
  return apiFetch('/api/customers/me/reviews');
}

export interface SubmitReviewRequest {
  productId: string;
  skuId: string;
  rating: number;
  title?: string | null;
  body: string;
}
export function submitReview(
  request: SubmitReviewRequest,
): Promise<{ reviewId: string; status: string }> {
  return apiFetch('/api/customers/me/reviews', { method: 'POST', body: request });
}

export interface EditReviewRequest {
  rating: number;
  title?: string | null;
  body: string;
}
export function editReview(
  reviewId: string,
  request: EditReviewRequest,
): Promise<{ status: string }> {
  return apiFetch(`/api/customers/me/reviews/${encodeURIComponent(reviewId)}`, {
    method: 'PUT',
    body: request,
  });
}

export function withdrawReview(reviewId: string): Promise<void> {
  return apiFetch(`/api/customers/me/reviews/${encodeURIComponent(reviewId)}`, {
    method: 'DELETE',
  });
}

export interface WishlistItem {
  skuId: string;
  productId: string;
  slug: string;
  productName: string;
  skuLabel: string;
  price: Money | null;
  availability: Availability;
  /** False for an archived/unlisted product or a discontinued SKU — still on the list. */
  onSale: boolean;
  addedOn: string;
  primaryImage: ProductImageRef | null;
}

export function getWishlist(): Promise<{ items: WishlistItem[] }> {
  return apiFetch('/api/customers/me/wishlist');
}
export function addWishlistItem(skuId: string): Promise<void> {
  return apiFetch(`/api/customers/me/wishlist/items/${encodeURIComponent(skuId)}`, {
    method: 'PUT',
  });
}
export function removeWishlistItem(skuId: string): Promise<void> {
  return apiFetch(`/api/customers/me/wishlist/items/${encodeURIComponent(skuId)}`, {
    method: 'DELETE',
  });
}

export type StockAlertStatus = 'Pending' | 'Notified' | 'Closed';
export interface StockAlert {
  stockAlertId: string;
  skuId: string;
  slug: string | null;
  productName: string | null;
  skuLabel: string | null;
  status: StockAlertStatus;
  requestedOn: string;
  notifiedOn: string | null;
}
export interface StockAlertDeliverability {
  willNotify: boolean;
  channels: string[];
  inboxCopy: boolean;
}

export function getStockAlerts(): Promise<StockAlert[]> {
  return apiFetch('/api/customers/me/stock-alerts');
}
export function requestStockAlert(
  skuId: string,
): Promise<{ stockAlertId: string; deliverability: StockAlertDeliverability }> {
  return apiFetch('/api/customers/me/stock-alerts', { method: 'POST', body: { skuId } });
}
export function cancelStockAlert(stockAlertId: string): Promise<void> {
  return apiFetch(`/api/customers/me/stock-alerts/${encodeURIComponent(stockAlertId)}`, {
    method: 'DELETE',
  });
}

export type RecommendationReasonCode =
  'LifeStageMatch' | 'Sterilised' | 'WeightManagement' | 'ConditionMatch';
export interface FoodRecommendations {
  cat: { catId: string; name: string; lifeStage: string | null; lifeStageIsEstimated: boolean };
  items: Array<{
    item: CatalogueItem;
    reasons: Array<{ code: RecommendationReasonCode | string; values: Record<string, string> }>;
  }>;
  excludedForAllergy: Array<{ allergen: string; count: number }>;
  /** `AddDateOfBirth`, `IngredientsUnknownExcluded`. */
  hints: string[];
}

export function getFoodRecommendations(catId: string, lang?: string): Promise<FoodRecommendations> {
  const query = lang ? `?locale=${encodeURIComponent(lang)}` : '';
  return apiFetch(
    `/api/customers/me/cats/${encodeURIComponent(catId)}/food-recommendations${query}`,
  );
}
