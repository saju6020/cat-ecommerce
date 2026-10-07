import { clearPersisted, loadPersisted, savePersisted } from '../admin/fixtures';
import {
  COUNTRY_NAMES,
  seedOrigins,
  seedProducts,
  seedReviews,
  shopFixtureIds,
  type Availability,
  type MockOrigin,
  type MockProduct,
  type MockReview,
  type MockSku,
} from './fixtures';

/**
 * `mocks/handlers/shop/store.ts` — HO-SHP-002 §12. The shop mock's in-memory database and the view
 * builders that shape it into HO-SHP-001 §12.4's wire models. Persisted to `sessionStorage` in a
 * real browser (the same pattern as `mocks/handlers/admin/fixtures.ts`), so a full-page
 * navigation keeps a mutation; in Vitest and in `msw/node` (server rendering) it is plain module
 * state, always seeded fresh.
 */

const PERSIST_KEY = '__purrcare_shop_mock_db__';

export interface MockAlert {
  stockAlertId: string;
  skuId: string;
  status: 'Pending' | 'Notified' | 'Closed';
  requestedOn: string;
  notifiedOn: string | null;
}
export interface MockWishlistEntry {
  skuId: string;
  addedOn: string;
}
interface Shape {
  products: MockProduct[];
  origins: MockOrigin[];
  reviews: MockReview[];
  wishlist: MockWishlistEntry[];
  alerts: MockAlert[];
}

function seedWishlist(): MockWishlistEntry[] {
  return [
    { skuId: shopFixtureIds.skuKitten400, addedOn: '2026-10-01T10:00:00Z' },
    { skuId: 'b0000000-0000-0000-0000-000000009001', addedOn: '2026-09-15T10:00:00Z' },
  ];
}

function seedAlerts(): MockAlert[] {
  return [
    {
      stockAlertId: 'e0000000-0000-0000-0000-000000000001',
      skuId: shopFixtureIds.skuKitten4kg,
      status: 'Pending',
      requestedOn: '2026-10-02T10:00:00Z',
      notifiedOn: null,
    },
    {
      stockAlertId: 'e0000000-0000-0000-0000-000000000002',
      skuId: 'b0000000-0000-0000-0000-000000000801',
      status: 'Notified',
      requestedOn: '2026-09-20T10:00:00Z',
      notifiedOn: '2026-10-03T08:00:00Z',
    },
    {
      stockAlertId: 'e0000000-0000-0000-0000-000000000003',
      skuId: 'b0000000-0000-0000-0000-000000009101',
      status: 'Closed',
      requestedOn: '2026-08-20T10:00:00Z',
      notifiedOn: null,
    },
  ];
}

function seed(): Shape {
  return {
    products: seedProducts(),
    origins: seedOrigins(),
    reviews: seedReviews(),
    wishlist: seedWishlist(),
    alerts: seedAlerts(),
  };
}

export const db: Shape = loadPersisted<Shape>(PERSIST_KEY) ?? seed();

export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetShopMockData(): void {
  Object.assign(db, seed());
  clearPersisted(PERSIST_KEY);
}

// ---- view builders ---------------------------------------------------------------------------

export const money = (amount: string) => ({ amount, currencyCode: 'BDT' });

export const isOnStorefront = (p: MockProduct) => p.status === 'Active' && p.listed;

export function findBySlug(slug: string): MockProduct | undefined {
  return db.products.find((p) => p.slug === slug);
}
export function findById(productId: string): MockProduct | undefined {
  return db.products.find((p) => p.productId === productId);
}
export function findSku(skuId: string): { product: MockProduct; sku: MockSku } | undefined {
  for (const product of db.products) {
    const sku = product.skus.find((s) => s.skuId === skuId);
    if (sku) return { product, sku };
  }
  return undefined;
}

const activeSkus = (p: MockProduct) => p.skus.filter((s) => s.status === 'Active');

export function skuAvailability(p: MockProduct, sku: MockSku): Availability {
  if (p.kind === 'Bundle' && p.bundle) {
    const allIn = p.bundle.every((c) => {
      const found = findSku(c.skuId);
      return found ? found.sku.inStock && found.sku.status === 'Active' : false;
    });
    return allIn ? 'InStock' : 'OutOfStock';
  }
  return sku.inStock ? 'InStock' : 'OutOfStock';
}

export function productAvailability(p: MockProduct): Availability {
  return activeSkus(p).some((s) => skuAvailability(p, s) === 'InStock') ? 'InStock' : 'OutOfStock';
}

const imageFor = (p: MockProduct, isPrimary: boolean, skuId: string | null = null) => ({
  url: null,
  altText: `${p.name} — product photo`,
  isPrimary,
  skuId,
});

export function priceRange(p: MockProduct): { from: string | null; varies: boolean } {
  const prices = activeSkus(p)
    .map((s) => s.price)
    .filter((x): x is string => x !== null);
  if (prices.length === 0) return { from: null, varies: false };
  const sorted = [...prices].sort((a, b) => Number(a) - Number(b));
  return { from: sorted[0]!, varies: new Set(prices).size > 1 };
}

export function catalogueItem(p: MockProduct) {
  const { from, varies } = priceRange(p);
  return {
    productId: p.productId,
    slug: p.slug,
    name: p.name,
    brand: p.brand,
    category: p.category,
    subcategory: p.subcategory,
    kind: p.kind,
    countryOfOrigin: p.country
      ? { code: p.country, name: COUNTRY_NAMES[p.country] ?? p.country }
      : null,
    lifeStage: p.lifeStage,
    prescriptionRequired: p.prescriptionRequired,
    fromPrice: from ? money(from) : null,
    priceVaries: varies,
    availability: productAvailability(p),
    rating: { average: p.ratingAverage, count: p.ratingCount },
    primaryImage: imageFor(p, true),
  };
}

export function detailView(p: MockProduct, locale: string | undefined) {
  const translation = locale ? p.translations.find((t) => t.locale === locale) : undefined;
  const pick = (
    field: 'name' | 'description' | 'ingredientList' | 'feedingGuide',
    base: string | null,
  ) => {
    const translated = translation?.[field] ?? null;
    return { value: translated ?? base, locale: translated ? locale! : 'en' };
  };
  const name = pick('name', p.name);
  const description = pick('description', p.description);
  const ingredientList = pick('ingredientList', p.ingredientList);
  const feedingGuide = pick('feedingGuide', p.feedingGuide);
  const isFood = p.category === 'CatFood';
  return {
    productId: p.productId,
    slug: p.slug,
    name: name.value!,
    brand: p.brand,
    description: description.value ?? '',
    category: p.category,
    subcategory: p.subcategory,
    tags: p.tags,
    kind: p.kind,
    countryOfOrigin: p.country
      ? { code: p.country, name: COUNTRY_NAMES[p.country] ?? p.country }
      : null,
    prescriptionRequired: p.prescriptionRequired,
    food:
      isFood && p.kind === 'Standard'
        ? {
            manufacturer: p.manufacturer,
            lifeStage: p.lifeStage,
            proteinSource: p.protein,
            ingredientList: ingredientList.value,
            guaranteedAnalysis: p.analysis,
            feedingGuide: feedingGuide.value,
            storageInstructions: p.storageInstructions,
          }
        : null,
    images: [imageFor(p, true), { ...imageFor(p, false), altText: `${p.name} — back of pack` }],
    skus: activeSkus(p).map((s) => ({
      skuId: s.skuId,
      code: s.code,
      label: s.sizeOrWeight ?? s.flavour ?? s.colour ?? s.code,
      sizeOrWeight: s.sizeOrWeight,
      flavour: s.flavour,
      colour: s.colour,
      packQuantity: s.packQuantity,
      price: s.price ? money(s.price) : null,
      availability: skuAvailability(p, s),
      expiryNotice: s.expiresOn && s.inStock ? { expiresOn: s.expiresOn } : null,
      images:
        s.code === 'RC-K-4KG'
          ? [{ ...imageFor(p, true, s.skuId), altText: `${p.name} 4 kg bag` }]
          : [],
    })),
    rating: { average: p.ratingAverage, count: p.ratingCount },
    pricesIncludeTax: false,
    locale: locale && translation ? locale : 'en',
    fieldLocales: {
      name: name.locale,
      description: description.locale,
      ingredientList: ingredientList.locale,
      feedingGuide: feedingGuide.locale,
    },
    bundle:
      p.kind === 'Bundle' && p.bundle
        ? {
            components: p.bundle.map((c) => {
              const found = findSku(c.skuId);
              return {
                productName: found?.product.name ?? 'Unknown product',
                productSlug: found?.product.slug ?? c.productSlug,
                skuLabel: found?.sku.sizeOrWeight ?? found?.sku.code ?? '',
                quantity: c.quantity,
                availability: found && found.sku.inStock ? 'InStock' : 'OutOfStock',
              };
            }),
          }
        : null,
  };
}

export function currentPrice(sku: MockSku) {
  return sku.price ? money(sku.price) : null;
}
