import { apiFetch, getApiUrl } from './client';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';
import type { ProductCategory, ProductKind } from './shop-catalogue';

/**
 * `lib/api/staff-shop.ts` — HO-SHP-002 §8.3. The catalogue back office (HO-SHP-001 §12.4 "Staff").
 * Types mirror the real view models and request contracts (`Api/Contracts/Product/*`). Every
 * product write carries `expectedVersion`; a stale one is a `409` (`ApiConflictError`).
 *
 * Contract note: the staff detail read returns the food labelling, images, health focus and SKU
 * label since HO-SHP-001-FU1 (backlog S1), so the editors load the stored values and re-save them
 * unchanged. `stock` per SKU is still deferred to the HO-IMP-001 follow-up slice (backlog SHP row
 * 51); it is typed optional.
 */

export type ProductStatus = 'Draft' | 'Active' | 'Archived';

export interface StaffProductSummary {
  productId: string;
  name: string;
  brand: string;
  category: ProductCategory;
  status: ProductStatus;
  kind: ProductKind;
  skuCount: number;
  listedOnStorefront: boolean;
  lastUpdatedOn: string;
}

export interface StaffProductListParams {
  q?: string;
  status?: string;
  category?: string;
  brand?: string;
  barcode?: string;
  kind?: string;
  listed?: string;
  sort?: string;
  pageNumber?: number;
  pageSize?: number;
}

function listQuery(params: StaffProductListParams): URLSearchParams {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '' && value !== null) query.set(key, String(value));
  }
  return query;
}

export function getStaffProducts(
  params: StaffProductListParams,
): Promise<PagedResult<StaffProductSummary>> {
  const query = listQuery(params).toString();
  return apiFetch(`/api/staff/shop/products${query ? `?${query}` : ''}`);
}

/** The server CSV export with the same filters (UI-4) — a plain link target, not a fetch. */
export function staffProductsExportUrl(params: StaffProductListParams): string {
  const { pageNumber: _page, pageSize: _size, barcode: _barcode, ...filters } = params;
  void _page;
  void _size;
  void _barcode;
  const query = listQuery(filters).toString();
  return getApiUrl(`/api/staff/shop/products/export.csv${query ? `?${query}` : ''}`);
}

export interface PublishChecklistItem {
  item: string;
  isBlocking: boolean;
  isMet: boolean;
  message: string;
}
export interface StaffSku {
  skuId: string;
  code: string;
  barcode: string | null;
  sizeOrWeight: string | null;
  flavour: string | null;
  colour: string | null;
  packQuantity: number | null;
  /** The public label: size, flavour, colour and "Pack of N" joined with " · "; the product name when none is set. */
  label: string;
  status: 'Active' | 'Discontinued';
  currentPrice: Money | null;
  scheduledPrice: Money | null;
  /** Deferred to the HO-IMP-001 follow-up (backlog SHP row 51). `null`/absent = never reported. */
  stock?: { sellableQuantity: number; nextAllocatableExpiryOn: string | null; asOf: string } | null;
}
export interface StaffBundleComponent {
  componentSkuId: string;
  skuCode: string | null;
  productName: string | null;
  quantity: number;
  isOnSale: boolean;
}
export interface StaffRelatedProduct {
  relatedProductId: string;
  kind: 'FrequentlyBoughtTogether' | 'UpSell';
  sortOrder: number;
  name: string | null;
  status: string | null;
}
export interface StaffTranslation {
  locale: string;
  name: string | null;
  description: string | null;
  ingredientList: string | null;
  feedingGuide: string | null;
}
export interface StaffFoodDetail {
  manufacturer: string | null;
  lifeStage: string | null;
  proteinSource: string | null;
  ingredientList: string | null;
  guaranteedAnalysis: {
    proteinPercent: number;
    fatPercent: number;
    fibrePercent: number;
    moisturePercent: number;
  } | null;
  feedingGuide: string | null;
  storageInstructions: string | null;
}
export interface StaffImage {
  fileId: string;
  /** Always `null` for now (no media URL builder yet). */
  url?: string | null;
  skuId: string | null;
  altText: string;
  sortOrder: number;
  isPrimary: boolean;
}

export interface StaffProductDetail {
  productId: string;
  version: number;
  kind: ProductKind;
  name: string;
  brand: string;
  description: string;
  category: ProductCategory;
  subcategory: string | null;
  tags: string[];
  slug: string;
  status: ProductStatus;
  firstPublishedOn: string | null;
  taxClassId: string;
  countryOfOriginCode: string | null;
  prescriptionRequired: boolean;
  isExpiryTracked: boolean;
  listedOnStorefront: boolean;
  skus: StaffSku[];
  publishChecklist: PublishChecklistItem[];
  bundleComponents?: StaffBundleComponent[] | null;
  relatedProducts?: StaffRelatedProduct[] | null;
  translations?: StaffTranslation[] | null;
  /** `null` when the product has no food details. Valid input to `PUT …/food`. */
  food: StaffFoodDetail | null;
  /** Ordered by `sortOrder`. Valid input to `PUT …/images`. */
  images: StaffImage[];
  /** Enumeration names; goes back with the Details `PUT`. */
  healthFocus: string[];
}

const productPath = (productId: string) =>
  `/api/staff/shop/products/${encodeURIComponent(productId)}`;

export function getStaffProduct(productId: string): Promise<StaffProductDetail> {
  return apiFetch(productPath(productId));
}

export interface CreateProductRequest {
  kind: ProductKind;
  name: string;
  brand: string;
  category: string;
  subcategory?: string | null;
  taxClassId: string;
  slug: string;
  bundleSkuCode?: string | null;
}
export function createProduct(request: CreateProductRequest): Promise<{ productId: string }> {
  return apiFetch('/api/staff/shop/products', { method: 'POST', body: request });
}

export interface UpdateProductDetailsRequest {
  expectedVersion: number;
  name: string;
  brand: string;
  description?: string | null;
  category: string;
  subcategory?: string | null;
  tags: string[];
  taxClassId: string;
  countryOfOriginCode?: string | null;
  listedOnStorefront: boolean;
  isExpiryTracked: boolean;
  healthFocus: string[];
}
export function updateProductDetails(
  productId: string,
  request: UpdateProductDetailsRequest,
): Promise<void> {
  return apiFetch(productPath(productId), { method: 'PUT', body: request });
}

export function changeProductSlug(
  productId: string,
  request: { expectedVersion: number; slug: string },
): Promise<void> {
  return apiFetch(`${productPath(productId)}/slug`, { method: 'PUT', body: request });
}

export interface SetFoodDetailsRequest {
  expectedVersion: number;
  manufacturer?: string | null;
  lifeStage?: string | null;
  proteinSource?: string | null;
  ingredientList?: string | null;
  guaranteedAnalysis?: {
    proteinPercent: number;
    fatPercent: number;
    fibrePercent: number;
    moisturePercent: number;
  } | null;
  feedingGuide?: string | null;
  storageInstructions?: string | null;
}
export function setFoodDetails(productId: string, request: SetFoodDetailsRequest): Promise<void> {
  return apiFetch(`${productPath(productId)}/food`, { method: 'PUT', body: request });
}

export function setPrescriptionRequired(
  productId: string,
  request: { expectedVersion: number; prescriptionRequired: boolean },
): Promise<void> {
  return apiFetch(`${productPath(productId)}/prescription`, { method: 'PUT', body: request });
}

export interface ProductImageInput {
  fileId: string;
  skuId?: string | null;
  altText: string;
  sortOrder: number;
  isPrimary: boolean;
}
export function setProductImages(
  productId: string,
  request: { expectedVersion: number; images: ProductImageInput[] },
): Promise<void> {
  return apiFetch(`${productPath(productId)}/images`, { method: 'PUT', body: request });
}

export function setBundleComponents(
  productId: string,
  request: {
    expectedVersion: number;
    components: Array<{ componentSkuId: string; quantity: number }>;
  },
): Promise<void> {
  return apiFetch(`${productPath(productId)}/bundle-components`, { method: 'PUT', body: request });
}

export function setRelatedProducts(
  productId: string,
  request: {
    expectedVersion: number;
    links: Array<{ relatedProductId: string; kind: string; sortOrder: number }>;
  },
): Promise<void> {
  return apiFetch(`${productPath(productId)}/related-products`, { method: 'PUT', body: request });
}

export interface TranslationInput {
  expectedVersion: number;
  name?: string | null;
  description?: string | null;
  ingredientList?: string | null;
  feedingGuide?: string | null;
}
export function setProductTranslation(
  productId: string,
  locale: string,
  request: TranslationInput,
): Promise<void> {
  return apiFetch(`${productPath(productId)}/translations/${encodeURIComponent(locale)}`, {
    method: 'PUT',
    body: request,
  });
}
export function removeProductTranslation(
  productId: string,
  locale: string,
  expectedVersion: number,
): Promise<void> {
  return apiFetch(
    `${productPath(productId)}/translations/${encodeURIComponent(locale)}?expectedVersion=${expectedVersion}`,
    { method: 'DELETE' },
  );
}

export interface SkuAttributesInput {
  expectedVersion: number;
  sizeOrWeight?: string | null;
  flavour?: string | null;
  colour?: string | null;
  packQuantity?: number | null;
}
export interface AddSkuRequest extends SkuAttributesInput {
  code: string;
  barcode?: string | null;
}
export function addSku(productId: string, request: AddSkuRequest): Promise<{ skuId: string }> {
  return apiFetch(`${productPath(productId)}/skus`, { method: 'POST', body: request });
}
export function updateSku(
  productId: string,
  skuId: string,
  request: SkuAttributesInput,
): Promise<void> {
  return apiFetch(`${productPath(productId)}/skus/${encodeURIComponent(skuId)}`, {
    method: 'PUT',
    body: request,
  });
}
export function discontinueSku(
  productId: string,
  skuId: string,
  expectedVersion: number,
): Promise<void> {
  return apiFetch(`${productPath(productId)}/skus/${encodeURIComponent(skuId)}/discontinue`, {
    method: 'POST',
    body: { expectedVersion },
  });
}
export function reactivateSku(
  productId: string,
  skuId: string,
  expectedVersion: number,
): Promise<void> {
  return apiFetch(`${productPath(productId)}/skus/${encodeURIComponent(skuId)}/reactivate`, {
    method: 'POST',
    body: { expectedVersion },
  });
}

export type ProductTransition = 'publish' | 'unpublish' | 'archive';
export function transitionProduct(
  productId: string,
  transition: ProductTransition,
  expectedVersion: number,
): Promise<void> {
  return apiFetch(`${productPath(productId)}/${transition}`, {
    method: 'POST',
    body: { expectedVersion },
  });
}

export interface PriceEntry {
  skuPriceId: string;
  amount: Money;
  effectiveFrom: string;
  setBy: string;
}
export interface SkuPriceHistory {
  current: PriceEntry | null;
  scheduled: PriceEntry[];
  past: PriceEntry[];
}
export function getSkuPrices(skuId: string): Promise<SkuPriceHistory> {
  return apiFetch(`/api/staff/shop/skus/${encodeURIComponent(skuId)}/prices`);
}
export function scheduleSkuPrice(
  skuId: string,
  request: { amount: number; currencyCode: string; effectiveFrom: string },
): Promise<{ skuPriceId: string }> {
  return apiFetch(`/api/staff/shop/skus/${encodeURIComponent(skuId)}/prices`, {
    method: 'POST',
    body: request,
  });
}

export interface SkuByBarcode {
  skuId: string;
  code: string;
  label: string;
  productId: string;
  productName: string;
  status: string;
}
export function findSkuByBarcode(barcode: string): Promise<SkuByBarcode> {
  return apiFetch(`/api/staff/shop/skus?barcode=${encodeURIComponent(barcode)}`);
}

export function getStaffCountries(): Promise<Array<{ code: string; name: string }>> {
  return apiFetch('/api/staff/shop/countries');
}

export interface StaffOrigin {
  countryCode: string;
  countryName: string;
  sourcingNotes: string | null;
  isFeatured: boolean;
  sortOrder: number;
  productCount: number;
}
export function getStaffOrigins(): Promise<StaffOrigin[]> {
  return apiFetch('/api/staff/shop/origins');
}
export interface OriginInput {
  sourcingNotes?: string | null;
  isFeatured: boolean;
  sortOrder: number;
}
export function createOrigin(
  request: OriginInput & { countryCode: string },
): Promise<{ countryCode: string }> {
  return apiFetch('/api/staff/shop/origins', { method: 'POST', body: request });
}
export function updateOrigin(countryCode: string, request: OriginInput): Promise<void> {
  return apiFetch(`/api/staff/shop/origins/${encodeURIComponent(countryCode)}`, {
    method: 'PUT',
    body: request,
  });
}

export type ModerationStatus = 'PendingModeration' | 'Published' | 'Rejected' | 'Hidden';
export interface ModerationItem {
  reviewId: string;
  productId: string;
  productName: string;
  skuLabel: string;
  rating: number;
  title: string | null;
  body: string;
  customerDisplayName: string;
  submittedOn: string;
  status: ModerationStatus;
}
export function getModerationQueue(params: {
  status?: string;
  productId?: string;
  pageNumber?: number;
}): Promise<PagedResult<ModerationItem>> {
  const query = new URLSearchParams();
  if (params.status) query.set('status', params.status);
  if (params.productId) query.set('productId', params.productId);
  if (params.pageNumber && params.pageNumber > 1) {
    query.set('pageNumber', String(params.pageNumber));
  }
  const text = query.toString();
  return apiFetch(`/api/staff/shop/reviews${text ? `?${text}` : ''}`);
}
export function publishReview(reviewId: string): Promise<void> {
  return apiFetch(`/api/staff/shop/reviews/${encodeURIComponent(reviewId)}/publish`, {
    method: 'POST',
  });
}
export function rejectReview(
  reviewId: string,
  request: { reason: string; note?: string | null },
): Promise<void> {
  return apiFetch(`/api/staff/shop/reviews/${encodeURIComponent(reviewId)}/reject`, {
    method: 'POST',
    body: request,
  });
}
export function hideReview(reviewId: string): Promise<void> {
  return apiFetch(`/api/staff/shop/reviews/${encodeURIComponent(reviewId)}/hide`, {
    method: 'POST',
  });
}
