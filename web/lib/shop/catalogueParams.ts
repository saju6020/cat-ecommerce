/**
 * `lib/shop/catalogueParams.ts` — HO-SHP-002 §8.4, ADR-FE-031. The ONE parser for catalogue state:
 * search, every filter, sort and page live in URL search params; this module reads and writes
 * them. Unknown params are dropped; repeated values keep their order. The server is the only
 * filter engine — nothing here filters, sorts or counts catalogue data.
 */

export const CATALOGUE_SORTS = [
  'relevance',
  'newest',
  'price_asc',
  'price_desc',
  'popularity',
  'rating',
] as const;
export type CatalogueSort = (typeof CATALOGUE_SORTS)[number];

export const MULTI_PARAMS = ['brand', 'origin', 'lifeStage', 'protein'] as const;
export type MultiParam = (typeof MULTI_PARAMS)[number];

export interface CatalogueParams {
  q: string;
  category: string;
  subcategory: string;
  brand: string[];
  origin: string[];
  lifeStage: string[];
  protein: string[];
  minPrice: string;
  maxPrice: string;
  inStock: boolean;
  minRating: number | null;
  sort: CatalogueSort | null;
  pageNumber: number;
  /** Content language (ADR-FE-037) — `lang` on the storefront URL, `locale` on the API. */
  lang: string;
}

export const EMPTY_CATALOGUE_PARAMS: CatalogueParams = {
  q: '',
  category: '',
  subcategory: '',
  brand: [],
  origin: [],
  lifeStage: [],
  protein: [],
  minPrice: '',
  maxPrice: '',
  inStock: false,
  minRating: null,
  sort: null,
  pageNumber: 1,
  lang: '',
};

export type CatalogueParamsInput =
  URLSearchParams | Record<string, string | string[] | undefined> | null | undefined;

function valuesOf(input: CatalogueParamsInput, key: string): string[] {
  if (!input) return [];
  if (input instanceof URLSearchParams) return input.getAll(key);
  const value = input[key];
  if (value === undefined) return [];
  return Array.isArray(value) ? value : [value];
}

function firstOf(input: CatalogueParamsInput, key: string): string {
  return (valuesOf(input, key)[0] ?? '').trim();
}

function listOf(input: CatalogueParamsInput, key: string): string[] {
  const seen: string[] = [];
  for (const raw of valuesOf(input, key)) {
    const value = raw.trim();
    if (value && !seen.includes(value)) seen.push(value);
  }
  return seen;
}

const DECIMAL = /^\d{1,9}(\.\d{1,2})?$/;

export function parseCatalogueParams(input: CatalogueParamsInput): CatalogueParams {
  const sort = firstOf(input, 'sort');
  const rating = Number(firstOf(input, 'minRating'));
  const page = Number(firstOf(input, 'pageNumber'));
  const minPrice = firstOf(input, 'minPrice');
  const maxPrice = firstOf(input, 'maxPrice');
  return {
    q: firstOf(input, 'q').slice(0, 100),
    category: firstOf(input, 'category'),
    subcategory: firstOf(input, 'subcategory'),
    brand: listOf(input, 'brand'),
    origin: listOf(input, 'origin'),
    lifeStage: listOf(input, 'lifeStage'),
    protein: listOf(input, 'protein'),
    minPrice: DECIMAL.test(minPrice) ? minPrice : '',
    maxPrice: DECIMAL.test(maxPrice) ? maxPrice : '',
    inStock: firstOf(input, 'availability') === 'inStock',
    minRating: Number.isInteger(rating) && rating >= 1 && rating <= 5 ? rating : null,
    sort: (CATALOGUE_SORTS as readonly string[]).includes(sort) ? (sort as CatalogueSort) : null,
    pageNumber: Number.isInteger(page) && page > 1 ? page : 1,
    lang: firstOf(input, 'lang'),
  };
}

/** The storefront URL's query string (canonical key order, defaults omitted). */
export function toSearchParams(params: CatalogueParams): URLSearchParams {
  const out = new URLSearchParams();
  if (params.q) out.set('q', params.q);
  if (params.category) out.set('category', params.category);
  if (params.subcategory) out.set('subcategory', params.subcategory);
  for (const key of MULTI_PARAMS) for (const value of params[key]) out.append(key, value);
  if (params.minPrice) out.set('minPrice', params.minPrice);
  if (params.maxPrice) out.set('maxPrice', params.maxPrice);
  if (params.inStock) out.set('availability', 'inStock');
  if (params.minRating !== null) out.set('minRating', String(params.minRating));
  if (params.sort) out.set('sort', params.sort);
  if (params.pageNumber > 1) out.set('pageNumber', String(params.pageNumber));
  if (params.lang) out.set('lang', params.lang);
  return out;
}

/** The API query string for `GET /api/shop/products` (`lang` becomes `locale`). */
export function toApiSearchParams(params: CatalogueParams, pageSize?: number): URLSearchParams {
  const out = toSearchParams({ ...params, lang: '' });
  if (params.pageNumber > 1) out.set('pageNumber', String(params.pageNumber));
  if (pageSize) out.set('pageSize', String(pageSize));
  if (params.lang) out.set('locale', params.lang);
  return out;
}

export function toQueryString(params: CatalogueParams): string {
  const query = toSearchParams(params).toString();
  return query ? `?${query}` : '';
}

/** A filter change always starts again from page 1; sort and every other filter are kept. */
export function withFilterChange(
  params: CatalogueParams,
  patch: Partial<CatalogueParams>,
): CatalogueParams {
  return { ...params, ...patch, pageNumber: 1 };
}

export function toggleValue(list: string[], value: string): string[] {
  return list.includes(value) ? list.filter((item) => item !== value) : [...list, value];
}

/** How many filters (not search, sort, page or language) are active — the sheet's `Filters (n)`. */
export function activeFilterCount(params: CatalogueParams): number {
  return (
    params.brand.length +
    params.origin.length +
    params.lifeStage.length +
    params.protein.length +
    (params.category ? 1 : 0) +
    (params.subcategory ? 1 : 0) +
    (params.minPrice || params.maxPrice ? 1 : 0) +
    (params.inStock ? 1 : 0) +
    (params.minRating !== null ? 1 : 0)
  );
}

/** "Clear all" — drops every filter but keeps the search text, sort and language. */
export function clearFilters(
  params: CatalogueParams,
  keep: Partial<CatalogueParams> = {},
): CatalogueParams {
  return {
    ...EMPTY_CATALOGUE_PARAMS,
    q: params.q,
    sort: params.sort,
    lang: params.lang,
    ...keep,
  };
}

/** The params a link or URL should carry: the page-owned (`fixed`) values are emptied, because the
 *  page's path already says them (an origin page's country). */
export function withoutFixed(
  params: CatalogueParams,
  fixed: Partial<CatalogueParams>,
): CatalogueParams {
  const next: CatalogueParams = { ...params };
  for (const key of Object.keys(fixed) as Array<keyof CatalogueParams>) {
    Object.assign(next, { [key]: EMPTY_CATALOGUE_PARAMS[key] });
  }
  return next;
}
