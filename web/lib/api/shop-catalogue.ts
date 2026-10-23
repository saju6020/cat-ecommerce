import { apiFetch, getPublic } from './client';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';
import { toApiSearchParams, type CatalogueParams } from '@/lib/shop/catalogueParams';

/**
 * `lib/api/shop-catalogue.ts` — HO-SHP-002 §8.3. The PUBLIC catalogue reads (HO-SHP-001 §12.4).
 * Types mirror the real view models (`src/Read/**\/ViewModels`) field for field. The `…Server`
 * functions are for Server Components and go through `getPublic`; the rest are for client islands
 * and go through `apiFetch`. Plain-text fields (names, brands, review text, ingredient list,
 * storage instructions) must be rendered as text; only `description`, `feedingGuide` and an
 * origin's `sourcingNotes` are LimitedMarkup (HO-SHP-001 backlog SHP row 22).
 */

export type Availability = 'InStock' | 'OutOfStock';
export type ProductKind = 'Standard' | 'Bundle';
export type ProductCategory =
  | 'CatFood'
  | 'Litter'
  | 'LitterPotsAndAccessories'
  | 'BagsAndCarriers'
  | 'HealthAndGrooming'
  | 'ToysAndFurniture';

export interface CountryRef {
  code: string;
  name: string;
}
export interface RatingSummary {
  average: number | null;
  count: number;
}
/** `url` is always `null` until HO-FIL-001 signed URLs are wired (backlog SHP row 2). */
export interface ProductImageRef {
  url: string | null;
  altText: string;
  isPrimary: boolean;
  skuId?: string | null;
}

export interface CatalogueItem {
  productId: string;
  slug: string;
  name: string;
  brand: string;
  category: ProductCategory;
  subcategory: string | null;
  kind: ProductKind;
  countryOfOrigin: CountryRef | null;
  lifeStage: string | null;
  prescriptionRequired: boolean;
  fromPrice: Money | null;
  priceVaries: boolean;
  availability: Availability;
  rating: RatingSummary;
  primaryImage: ProductImageRef | null;
  /** One card per size (owner, 2026-10-18): the size this card is, and its label ("400 g"). */
  skuId?: string | null;
  sizeLabel?: string | null;
}

export interface CatalogueResult extends PagedResult<CatalogueItem> {
  appliedFilters: Record<string, string | null>;
  suggestions: { didYouMean: string[]; popular: CatalogueItem[] } | null;
  /** How many cards (sizes) the filtered list has; `totalCount` stays the product count paging uses. */
  totalItemCount?: number | null;
}

export interface CatalogueFilters {
  brands: Array<{ name: string; count: number }>;
  origins: Array<{ code: string; name: string; count: number }>;
  lifeStages: Array<{ value: string; count: number }>;
  proteinSources: Array<{ value: string; count: number }>;
  price: { min: number | null; max: number | null };
}

export interface CategoryNode {
  value: ProductCategory;
  productCount: number;
  subcategories: Array<{ value: string; productCount: number }>;
}

export interface Origin {
  code: string;
  name: string;
  sourcingNotes: string | null;
  productCount: number;
  isFeatured: boolean;
}

export interface GuaranteedAnalysis {
  proteinPercent: number;
  fatPercent: number;
  fibrePercent: number;
  moisturePercent: number;
}
export interface FoodDetail {
  manufacturer: string | null;
  lifeStage: string | null;
  proteinSource: string | null;
  ingredientList: string | null;
  guaranteedAnalysis: GuaranteedAnalysis | null;
  feedingGuide: string | null;
  storageInstructions: string | null;
}
export interface DetailSku {
  skuId: string;
  code: string;
  label: string;
  sizeOrWeight: string | null;
  flavour: string | null;
  colour: string | null;
  packQuantity: number | null;
  price: Money | null;
  availability: Availability;
  expiryNotice: { expiresOn: string } | null;
  images: ProductImageRef[];
}
export interface BundleComponent {
  productName: string;
  productSlug: string;
  skuLabel: string;
  quantity: number;
  availability: Availability;
}
export interface ProductDetail {
  productId: string;
  slug: string;
  name: string;
  brand: string;
  description: string;
  category: ProductCategory;
  subcategory: string | null;
  tags: string[];
  kind: ProductKind;
  countryOfOrigin: CountryRef | null;
  prescriptionRequired: boolean;
  food: FoodDetail | null;
  images: ProductImageRef[];
  skus: DetailSku[];
  rating: RatingSummary;
  pricesIncludeTax: boolean;
  locale: string;
  /** Per content field, the language it was actually served in (ADR-FE-037, US-SHP-14 AC2). */
  fieldLocales: Record<string, string>;
  bundle: { components: BundleComponent[] } | null;
}

export interface ProductReview {
  reviewId: string;
  rating: number;
  title: string | null;
  body: string;
  authorDisplayName: string;
  skuLabel: string;
  submittedOn: string;
  verifiedPurchase: boolean;
}

export type RecommendationBasis = 'YourCats' | 'Generic' | string;
export interface Recommendations {
  frequentlyBoughtTogether: CatalogueItem[];
  upSell: CatalogueItem[];
  lifeStage: { basis: RecommendationBasis; lifeStages: string[]; items: CatalogueItem[] };
}

export interface ShopLocales {
  defaultLocale: string;
  locales: string[];
}

const withQuery = (path: string, query: URLSearchParams) => {
  const text = query.toString();
  return text ? `${path}?${text}` : path;
};

function langQuery(lang?: string): URLSearchParams {
  const out = new URLSearchParams();
  if (lang) out.set('locale', lang);
  return out;
}

// ---- Server Component reads (getPublic) -------------------------------------------------------

// S12: the Data Cache keys on the full URL, and these reads are keyed on free-form visitor input
// (search text, brand, price, category, language), so each unique query string would add an
// entry that is never evicted. They are not cached (`revalidate: 0`); the API has its own caches.
const NOT_CACHED = { revalidate: 0 } as const;

export function getCatalogueServer(params: CatalogueParams): Promise<CatalogueResult> {
  return getPublic<CatalogueResult>(
    withQuery('/api/shop/products', toApiSearchParams(params)),
    NOT_CACHED,
  );
}

export function getCatalogueFiltersServer(category?: string): Promise<CatalogueFilters> {
  const query = new URLSearchParams();
  if (category) query.set('category', category);
  // Without a category there is one URL, so it may be cached; with one it is visitor input.
  return getPublic<CatalogueFilters>(
    withQuery('/api/shop/filters', query),
    category ? NOT_CACHED : {},
  );
}

export function getCategoriesServer(): Promise<CategoryNode[]> {
  return getPublic<CategoryNode[]>('/api/shop/categories');
}

export function getOriginsServer(): Promise<Origin[]> {
  return getPublic<Origin[]>('/api/shop/origins');
}

export function getOriginServer(countryCode: string): Promise<Origin> {
  return getPublic<Origin>(`/api/shop/origins/${encodeURIComponent(countryCode)}`);
}

export function getLocalesServer(): Promise<ShopLocales> {
  return getPublic<ShopLocales>('/api/shop/locales');
}

/**
 * S12: `?lang=` is visitor input and part of the cached URL, so only an enabled locale is passed on
 * (anything else, or a failed locale read, means the default language). The cache then holds at
 * most one entry per product and enabled language.
 */
export async function getProductDetailServer(slug: string, lang?: string): Promise<ProductDetail> {
  let validLang: string | undefined;
  if (lang) {
    try {
      const { locales } = await getLocalesServer();
      if (locales.includes(lang)) validLang = lang;
    } catch {
      validLang = undefined;
    }
  }
  // Not cached: the detail carries signed photo URLs that expire (files.signedUrlMinutes, 5 by default),
  // and a cached copy is served stale once after it expires — the photo then failed on that first visit.
  return getPublic<ProductDetail>(
    withQuery(`/api/shop/products/${encodeURIComponent(slug)}`, langQuery(validLang)),
    NOT_CACHED,
  );
}

export const MAX_REVIEW_PAGE = 500;

/** The review page number kept to a whole number from 1 to `MAX_REVIEW_PAGE` (S12). */
export function clampReviewPage(pageNumber: number): number {
  if (!Number.isFinite(pageNumber)) return 1;
  return Math.min(MAX_REVIEW_PAGE, Math.max(1, Math.trunc(pageNumber)));
}

export function getProductReviewsServer(
  slug: string,
  pageNumber = 1,
): Promise<PagedResult<ProductReview>> {
  const query = new URLSearchParams();
  const page = clampReviewPage(pageNumber);
  if (page > 1) query.set('pageNumber', String(page));
  return getPublic<PagedResult<ProductReview>>(
    withQuery(`/api/shop/products/${encodeURIComponent(slug)}/reviews`, query),
  );
}

// ---- Client island reads (apiFetch) -----------------------------------------------------------

export function getProductReviews(
  slug: string,
  pageNumber = 1,
): Promise<PagedResult<ProductReview>> {
  const query = new URLSearchParams();
  if (pageNumber > 1) query.set('pageNumber', String(pageNumber));
  return apiFetch<PagedResult<ProductReview>>(
    withQuery(`/api/shop/products/${encodeURIComponent(slug)}/reviews`, query),
  );
}

/** Fetched client-side so exclusions reflect the session (the cookie rides on `apiFetch`). */
export function getRecommendations(slug: string, lang?: string): Promise<Recommendations> {
  return apiFetch<Recommendations>(
    withQuery(`/api/shop/products/${encodeURIComponent(slug)}/recommendations`, langQuery(lang)),
  );
}

export function getCategories(): Promise<CategoryNode[]> {
  return apiFetch<CategoryNode[]>('/api/shop/categories');
}

// ---- Storefront shell reads (getPublic) -------------------------------------------------------

export interface StorefrontStatus {
  maintenanceMode: boolean;
  message: string | null;
}

/** `GET /api/system/status` — read server-side for `MaintenanceGate` (US-ADM-08). Short cache. */
export function getSystemStatusServer(): Promise<StorefrontStatus> {
  return getPublic<StorefrontStatus>('/api/system/status', { revalidate: 10 });
}

export interface StorefrontBanner {
  slug: string;
  title: string;
  body: string;
  placement: string;
  linkUrl: string | null;
}

export function getBannersServer(placement: 'Shop' | 'Home'): Promise<StorefrontBanner[]> {
  return getPublic<StorefrontBanner[]>(
    `/api/content/banners?placement=${encodeURIComponent(placement)}`,
  );
}

/** The same status read from the browser, for the client-side maintenance watch. */
export function getSystemStatus(): Promise<StorefrontStatus> {
  return apiFetch<StorefrontStatus>('/api/system/status');
}

/** Client-side product read (the review form needs the product id behind a slug). */
export function getProductDetail(slug: string): Promise<ProductDetail> {
  return apiFetch<ProductDetail>(`/api/shop/products/${encodeURIComponent(slug)}`);
}

/** The enabled content languages, from the browser (the translation editor). */
export function getShopLocales(): Promise<ShopLocales> {
  return apiFetch<ShopLocales>('/api/shop/locales');
}
