import { http, HttpResponse, type HttpHandler } from 'msw';
import { COUNTRY_NAMES, CATEGORY_TREE, badRequest, type MockProduct } from './fixtures';
import {
  catalogueItem,
  db,
  detailView,
  findBySlug,
  isOnStorefront,
  priceRange,
  productAvailability,
} from './store';

/**
 * `mocks/handlers/shop/catalogue.ts` — HO-SHP-002 §12. The PUBLIC catalogue endpoints of
 * HO-SHP-001 §12.4, filtered/sorted/searched here so the UI never has to (ADR-FE-031). The same
 * handlers serve the browser worker and `msw/node` (server rendering).
 */

export function hasCustomerSession(request: Request): boolean {
  const jar = typeof document !== 'undefined' ? document.cookie : '';
  if (/(?:^|;\s*)pc_session=/.test(jar)) return true;
  return /(?:^|;\s*)pc_session=/.test(request.headers.get('cookie') ?? '');
}

function distance(a: string, b: string): number {
  const rows = Array.from({ length: a.length + 1 }, (_, i) => [
    i,
    ...new Array<number>(b.length).fill(0),
  ]);
  for (let j = 1; j <= b.length; j += 1) rows[0]![j] = j;
  for (let i = 1; i <= a.length; i += 1) {
    for (let j = 1; j <= b.length; j += 1) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      rows[i]![j] = Math.min(
        rows[i - 1]![j]! + 1,
        rows[i]![j - 1]! + 1,
        rows[i - 1]![j - 1]! + cost,
      );
    }
  }
  return rows[a.length]![b.length]!;
}

const tokens = (text: string) =>
  text
    .toLowerCase()
    .split(/[^a-z0-9]+/)
    .filter(Boolean);

function searchWords(p: MockProduct): string[] {
  return tokens(
    [
      p.name,
      p.brand,
      p.tags.join(' '),
      p.ingredientList ?? '',
      p.protein ?? '',
      COUNTRY_NAMES[p.country ?? ''] ?? '',
    ].join(' '),
  );
}

function matchesQuery(p: MockProduct, q: string): boolean {
  const words = searchWords(p);
  const haystack = words.join(' ');
  return tokens(q).every((token) => {
    if (haystack.includes(token)) return true;
    const tolerance = token.length >= 5 ? 1 : 0;
    return tolerance > 0 && words.some((word) => distance(word, token) <= tolerance);
  });
}

function didYouMean(q: string): string[] {
  const vocabulary = [...new Set(db.products.flatMap((p) => tokens(`${p.brand} ${p.name}`)))];
  const fixed = tokens(q).map((token) => {
    const best = vocabulary
      .map((word) => ({ word, d: distance(word, token) }))
      .filter((x) => x.d <= 2)
      .sort((a, b) => a.d - b.d)[0];
    return best?.word ?? null;
  });
  if (fixed.length === 0 || fixed.some((word) => word === null)) return [];
  const suggestion = fixed.join(' ');
  return suggestion === q.toLowerCase() ? [] : [suggestion];
}

const minPriceOf = (p: MockProduct) => Number(priceRange(p).from ?? Number.MAX_SAFE_INTEGER);

export function storefrontProducts() {
  return db.products.filter(isOnStorefront);
}

function getList(url: URL) {
  const sp = url.searchParams;
  const q = (sp.get('q') ?? '').trim();
  const brands = sp.getAll('brand').map((b) => b.toLowerCase());
  const origins = sp.getAll('origin').map((o) => o.toUpperCase());
  const lifeStages = sp.getAll('lifeStage');
  const proteins = sp.getAll('protein');
  const minPrice = sp.get('minPrice');
  const maxPrice = sp.get('maxPrice');
  const minRating = Number(sp.get('minRating') ?? '0');

  let items = storefrontProducts().filter((p) => {
    if (q && !matchesQuery(p, q)) return false;
    if (sp.get('category') && p.category !== sp.get('category')) return false;
    if (sp.get('subcategory') && p.subcategory !== sp.get('subcategory')) return false;
    if (brands.length && !brands.includes(p.brand.toLowerCase())) return false;
    if (origins.length && !(p.country && origins.includes(p.country))) return false;
    if (
      lifeStages.length &&
      !(p.lifeStage && (lifeStages.includes(p.lifeStage) || p.lifeStage === 'AllLifeStages'))
    )
      return false;
    if (proteins.length && !(p.protein && proteins.includes(p.protein))) return false;
    if (minPrice || maxPrice) {
      const ok = p.skus.some((s) => {
        if (s.status !== 'Active' || !s.price) return false;
        const n = Number(s.price);
        return (!minPrice || n >= Number(minPrice)) && (!maxPrice || n <= Number(maxPrice));
      });
      if (!ok) return false;
    }
    if (sp.get('availability') === 'inStock' && productAvailability(p) !== 'InStock') return false;
    if (minRating && !((p.ratingAverage ?? 0) >= minRating)) return false;
    return true;
  });

  const sort = sp.get('sort') || (q ? 'relevance' : 'newest');
  const byNewest = (a: MockProduct, b: MockProduct) => b.createdAt.localeCompare(a.createdAt);
  items = [...items].sort((a, b) => {
    if (sort === 'price_asc') return minPriceOf(a) - minPriceOf(b);
    if (sort === 'price_desc') return minPriceOf(b) - minPriceOf(a);
    if (sort === 'popularity') return b.sold - a.sold;
    if (sort === 'rating') return (b.ratingAverage ?? -1) - (a.ratingAverage ?? -1);
    return byNewest(a, b);
  });

  const pageSize = Math.min(Number(sp.get('pageSize') ?? '24') || 24, 48);
  const pageNumber = Math.max(1, Number(sp.get('pageNumber') ?? '1') || 1);
  const page = items.slice((pageNumber - 1) * pageSize, pageNumber * pageSize);

  const popular = [...storefrontProducts()].sort((a, b) => b.sold - a.sold).slice(0, 8);
  return {
    items: page.map(catalogueItem),
    pageNumber,
    pageSize,
    totalCount: items.length,
    appliedFilters: {
      q: q || null,
      category: sp.get('category'),
      subcategory: sp.get('subcategory'),
      brand: brands.length ? sp.getAll('brand').join(',') : null,
      origin: origins.length ? origins.join(',') : null,
      lifeStage: lifeStages.length ? lifeStages.join(',') : null,
      protein: proteins.length ? proteins.join(',') : null,
      minPrice: minPrice ? Number(minPrice).toFixed(2) : null,
      maxPrice: maxPrice ? Number(maxPrice).toFixed(2) : null,
      availability: sp.get('availability') === 'inStock' ? 'inStock' : null,
      minRating: minRating ? String(minRating) : null,
      sort:
        sort === 'price_asc'
          ? 'PriceAscending'
          : sort === 'price_desc'
            ? 'PriceDescending'
            : sort.charAt(0).toUpperCase() + sort.slice(1),
    },
    suggestions:
      q && items.length === 0
        ? { didYouMean: didYouMean(q), popular: popular.map(catalogueItem) }
        : null,
  };
}

function countBy<T>(values: T[], key: (v: T) => string | null): Array<[string, number]> {
  const map = new Map<string, number>();
  for (const v of values) {
    const k = key(v);
    if (k) map.set(k, (map.get(k) ?? 0) + 1);
  }
  return [...map.entries()];
}

export const catalogueHandlers: HttpHandler[] = [
  http.get('*/api/shop/products', ({ request }) => {
    const url = new URL(request.url);
    const sp = url.searchParams;
    const min = sp.get('minPrice');
    const max = sp.get('maxPrice');
    if (min && max && Number(min) > Number(max)) {
      return badRequest(
        'minPrice',
        'InvalidCatalogueFilter',
        'minPrice cannot exceed maxPrice.',
        'Catalogue',
      );
    }
    if ((sp.get('q') ?? '').length > 100) {
      return badRequest(
        'q',
        'InvalidCatalogueFilter',
        'q cannot exceed 100 characters.',
        'Catalogue',
      );
    }
    return HttpResponse.json(getList(url), { headers: { 'Cache-Control': 'public, max-age=60' } });
  }),

  http.get('*/api/shop/filters', ({ request }) => {
    const category = new URL(request.url).searchParams.get('category');
    const scope = storefrontProducts().filter((p) => !category || p.category === category);
    const prices = scope.flatMap((p) =>
      p.skus.map((s) => (s.price ? Number(s.price) : null)).filter((n): n is number => n !== null),
    );
    return HttpResponse.json({
      brands: countBy(scope, (p) => p.brand)
        .map(([name, count]) => ({ name, count }))
        .sort((a, b) => a.name.localeCompare(b.name)),
      origins: countBy(scope, (p) => p.country).map(([code, count]) => ({
        code,
        name: COUNTRY_NAMES[code] ?? code,
        count,
      })),
      lifeStages: countBy(scope, (p) => p.lifeStage).map(([value, count]) => ({ value, count })),
      proteinSources: countBy(scope, (p) => p.protein).map(([value, count]) => ({ value, count })),
      price: {
        min: prices.length ? Math.min(...prices) : null,
        max: prices.length ? Math.max(...prices) : null,
      },
    });
  }),

  http.get('*/api/shop/categories', () => {
    const scope = storefrontProducts();
    return HttpResponse.json(
      Object.entries(CATEGORY_TREE).map(([value, subs]) => ({
        value,
        productCount: scope.filter((p) => p.category === value).length,
        subcategories: subs.map((sub) => ({
          value: sub,
          productCount: scope.filter((p) => p.category === value && p.subcategory === sub).length,
        })),
      })),
    );
  }),

  http.get('*/api/shop/origins', () => HttpResponse.json(originViews())),

  http.get('*/api/shop/origins/:countryCode', ({ params }) => {
    const found = originViews().find((o) => o.code === String(params.countryCode).toUpperCase());
    return found ? HttpResponse.json(found) : new HttpResponse(null, { status: 404 });
  }),

  // Locales: the mock always enables `bn` so the language switcher and the translation editor can
  // be exercised end to end; the real default is `["en"]` (SRS Q-12) and U-F2/U-D7 cover that case.
  http.get('*/api/shop/locales', () =>
    HttpResponse.json({ defaultLocale: 'en', locales: ['en', 'bn'] }),
  ),

  http.get('*/api/shop/products/:slug', ({ params, request }) => {
    const product = findBySlug(String(params.slug));
    if (!product || !isOnStorefront(product)) return new HttpResponse(null, { status: 404 });
    const locale = new URL(request.url).searchParams.get('locale') ?? undefined;
    return HttpResponse.json(detailView(product, locale));
  }),

  http.get('*/api/shop/products/:slug/reviews', ({ params, request }) => {
    const product = findBySlug(String(params.slug));
    if (!product || !isOnStorefront(product)) return new HttpResponse(null, { status: 404 });
    const pageNumber = Math.max(
      1,
      Number(new URL(request.url).searchParams.get('pageNumber') ?? '1') || 1,
    );
    const published = db.reviews
      .filter((r) => r.slug === product.slug && r.status === 'Published')
      .sort((a, b) => b.submittedOn.localeCompare(a.submittedOn));
    return HttpResponse.json({
      items: published.slice((pageNumber - 1) * 10, pageNumber * 10).map((r) => ({
        reviewId: r.reviewId,
        rating: r.rating,
        title: r.title,
        body: r.body,
        authorDisplayName: r.authorDisplayName,
        skuLabel: r.skuLabel,
        submittedOn: r.submittedOn,
        verifiedPurchase: true,
      })),
      pageNumber,
      pageSize: 10,
      totalCount: published.length,
    });
  }),

  http.get('*/api/shop/products/:slug/recommendations', ({ params, request }) => {
    const product = findBySlug(String(params.slug));
    if (!product || !isOnStorefront(product)) return new HttpResponse(null, { status: 404 });
    const linked = (kind: 'FrequentlyBoughtTogether' | 'UpSell') =>
      product.related
        .filter((r) => r.kind === kind)
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((r) => findBySlug(r.slug))
        .filter((p): p is MockProduct => !!p && isOnStorefront(p))
        .map(catalogueItem);
    const sameStage = storefrontProducts()
      .filter(
        (p) =>
          p.productId !== product.productId &&
          p.lifeStage &&
          p.lifeStage === product.lifeStage &&
          p.category === product.category &&
          !p.prescriptionRequired,
      )
      .slice(0, 4)
      .map(catalogueItem);
    const personal = hasCustomerSession(request);
    return HttpResponse.json(
      {
        frequentlyBoughtTogether: linked('FrequentlyBoughtTogether'),
        upSell: linked('UpSell'),
        lifeStage: {
          basis: personal ? 'YourCats' : 'Generic',
          lifeStages: product.lifeStage ? [product.lifeStage] : [],
          items: product.lifeStage ? sameStage : [],
        },
      },
      { headers: { 'Cache-Control': personal ? 'private, no-store' : 'public, max-age=60' } },
    );
  }),
];

function originViews() {
  const scope = storefrontProducts().filter((p) => p.category === 'CatFood');
  return [...db.origins]
    .map((o) => ({
      code: o.code,
      name: COUNTRY_NAMES[o.code] ?? o.code,
      sourcingNotes: o.sourcingNotes,
      productCount: scope.filter((p) => p.country === o.code).length,
      isFeatured: o.isFeatured,
      sortOrder: o.sortOrder,
    }))
    .filter((o) => o.productCount > 0)
    .sort(
      (a, b) =>
        Number(b.isFeatured) - Number(a.isFeatured) ||
        a.sortOrder - b.sortOrder ||
        a.name.localeCompare(b.name),
    )
    .map(({ sortOrder: _sortOrder, ...rest }) => {
      void _sortOrder;
      return rest;
    });
}
