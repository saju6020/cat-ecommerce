import { HttpResponse } from 'msw';
import { accountsFixtureIds } from '../areas/accounts';
import { catProfilesFixtureIds } from '../areas/cat-profiles';
import { administrationFixtureIds } from '../admin/fixtures';

/**
 * `mocks/handlers/shop/fixtures.ts` — HO-SHP-002 §12. Ids and the seeded catalogue, generated
 * from HO-SHP-001 §12.4 (never invented fields). Shared by the browser worker and `msw/node`
 * (server rendering in mock mode), so everything here is plain data with no browser dependency.
 *
 * Image `url` is ALWAYS `null`, exactly like the merged API (backlog SHP row 2) — the UI renders
 * the placeholder.
 */

const uuid = (prefix: string, n: number) =>
  `${prefix}0000000-0000-0000-0000-${String(n).padStart(12, '0')}`;

export const shopFixtureIds = {
  customerId: accountsFixtureIds.customerId,
  /** The staff tax class every seeded product uses (HO-ADM-001 list). */
  taxClassId: administrationFixtureIds.taxClassStandardId,
  catAllergy: catProfilesFixtureIds.catWithAllergiesExactDob, // Whiskers — chicken allergy
  catEstimatedDob: catProfilesFixtureIds.catNoAllergiesEstimatedDob, // Biscuit — about adult
  catNoDob: catProfilesFixtureIds.catCurrentPlain, // Mittens — AddDateOfBirth hint

  /** Three variants: 400 g and 2 kg in stock (2 kg carries an expiry notice), 4 kg out of stock. */
  multiVariantSlug: 'royal-canin-kitten',
  multiVariantProductId: uuid('a', 1),
  skuKitten400: uuid('b', 101),
  skuKitten2kg: uuid('b', 102),
  skuKitten4kg: uuid('b', 103),
  /** Thai kitten food — the AC-5 path. */
  thaiKittenSlug: 'whiskas-kitten-salmon',
  thaiKittenProductId: uuid('a', 2),
  skuWhiskasKitten: uuid('b', 201),
  /** Prescription diet — the signed-in customer has NO authorisation. */
  rxNoAuthSlug: 'hills-cd-urinary-care',
  rxNoAuthProductId: uuid('a', 5),
  skuRxNoAuth: uuid('b', 501),
  /** Prescription diet — the customer has two eligible cats. */
  rxEligibleSlug: 'hills-kd-renal-care',
  rxEligibleProductId: uuid('a', 6),
  skuRxEligible: uuid('b', 601),
  /** Kit whose second component is out of stock. */
  kitSlug: 'kitten-starter-kit',
  kitProductId: uuid('a', 7),
  skuKit: uuid('b', 701),
  /** A product the customer has bought and can review. */
  reviewableSlug: 'royal-canin-kitten',
  /** A product the customer already reviewed. */
  alreadyReviewedSlug: 'whiskas-kitten-salmon',
  /** Standing in for "a customer who granted no marketing consent": an alert here says it will not
   *  be delivered (`willNotify: false`). The real flag is the routing policy's, not a SKU's. */
  skuAlertNoConsent: uuid('b', 801),
  noConsentSlug: 'reflex-adult-chicken',
  /** Out-of-stock SKU whose alert is delivered. */
  skuAlertDelivered: uuid('b', 103),
  /** A Draft product (404 on the storefront) and an Active one with an unmet checklist. */
  draftProductId: uuid('a', 90),
  draftSlug: 'draft-hairball-gel',
  unmetChecklistProductId: uuid('a', 91),
  pendingReviewId: uuid('d', 1),
  publishedReviewId: uuid('d', 2),
  rejectedReviewId: uuid('d', 3),
  hiddenReviewId: uuid('d', 4),
  moderationPendingReviewId: uuid('d', 11),
  priceSkuId: uuid('b', 101),
} as const;

export type Availability = 'InStock' | 'OutOfStock';

export interface MockSku {
  skuId: string;
  code: string;
  barcode: string | null;
  sizeOrWeight: string | null;
  flavour: string | null;
  colour: string | null;
  packQuantity: number | null;
  status: 'Active' | 'Discontinued';
  /** Current price amount, or null when never priced. */
  price: string | null;
  scheduledPrice: { amount: string; effectiveFrom: string } | null;
  inStock: boolean;
  expiresOn: string | null;
}

export interface MockTranslation {
  locale: string;
  name: string | null;
  description: string | null;
  ingredientList: string | null;
  feedingGuide: string | null;
}

export interface MockProduct {
  productId: string;
  slug: string;
  version: number;
  name: string;
  brand: string;
  description: string;
  category: string;
  subcategory: string | null;
  kind: 'Standard' | 'Bundle';
  status: 'Draft' | 'Active' | 'Archived';
  listed: boolean;
  country: string | null;
  lifeStage: string | null;
  protein: string | null;
  prescriptionRequired: boolean;
  tags: string[];
  ingredientList: string | null;
  manufacturer: string | null;
  analysis: {
    proteinPercent: number;
    fatPercent: number;
    fibrePercent: number;
    moisturePercent: number;
  } | null;
  feedingGuide: string | null;
  storageInstructions: string | null;
  healthFocus: string[];
  images: Array<{
    fileId: string;
    altText: string;
    sortOrder: number;
    isPrimary: boolean;
    skuId: string | null;
  }>;
  skus: MockSku[];
  ratingAverage: number | null;
  ratingCount: number;
  createdAt: string;
  sold: number;
  bundle: Array<{ productSlug: string; skuId: string; quantity: number }> | null;
  related: Array<{ slug: string; kind: 'FrequentlyBoughtTogether' | 'UpSell'; sortOrder: number }>;
  translations: MockTranslation[];
  taxClassId: string;
  isExpiryTracked: boolean;
  firstPublishedOn: string | null;
}

export const COUNTRY_NAMES: Record<string, string> = {
  DE: 'Germany',
  TH: 'Thailand',
  TR: 'Türkiye',
  US: 'United States',
  KR: 'South Korea',
  FR: 'France',
  JP: 'Japan',
  IN: 'India',
  BD: 'Bangladesh',
};

export const CATEGORY_TREE: Record<string, string[]> = {
  CatFood: ['Dry', 'Wet', 'Treats', 'Kitten', 'Adult', 'Senior', 'Therapeutic'],
  Litter: ['Clumping', 'Silica', 'Tofu', 'Bentonite'],
  LitterPotsAndAccessories: ['Trays', 'HoodedBoxes', 'Scoops', 'Mats', 'Liners'],
  BagsAndCarriers: ['Carriers', 'Backpacks', 'TravelBags', 'StorageBags'],
  HealthAndGrooming: [],
  ToysAndFurniture: [],
};

export const MOCK_NOW = '2026-10-04T08:00:00Z';

const DESCRIPTION = [
  'A complete food made for **growing kittens**.',
  '',
  '- Supports healthy growth',
  '- Gentle on small tummies',
  '',
  'Read the [feeding guide](/shop) before changing diets.',
].join('\n');

const FEEDING_GUIDE = [
  '## Daily amounts',
  '',
  'Feed **40 g** per day for a 1 kg kitten, spread across 3 meals.',
  '',
  '- Always provide fresh water',
].join('\n');

function sku(
  n: number,
  code: string,
  label: string,
  price: string | null,
  inStock: boolean,
  extra: Partial<MockSku> = {},
): MockSku {
  return {
    skuId: uuid('b', n),
    code,
    barcode: `89${String(n).padStart(11, '0')}`,
    sizeOrWeight: label,
    flavour: null,
    colour: null,
    packQuantity: null,
    status: 'Active',
    price,
    scheduledPrice: null,
    inStock,
    expiresOn: null,
    ...extra,
  };
}

function product(
  p: Partial<MockProduct> & Pick<MockProduct, 'productId' | 'slug' | 'name' | 'brand' | 'skus'>,
): MockProduct {
  return {
    version: 1,
    description: DESCRIPTION,
    category: 'CatFood',
    subcategory: 'Dry',
    kind: 'Standard',
    status: 'Active',
    listed: true,
    country: null,
    lifeStage: null,
    protein: null,
    prescriptionRequired: false,
    tags: [],
    ingredientList: null,
    manufacturer: null,
    analysis: null,
    feedingGuide: null,
    storageInstructions: null,
    healthFocus: [],
    images: [],
    ratingAverage: null,
    ratingCount: 0,
    createdAt: '2026-06-01T00:00:00Z',
    sold: 0,
    bundle: null,
    related: [],
    translations: [],
    taxClassId: shopFixtureIds.taxClassId,
    isExpiryTracked: false,
    firstPublishedOn: '2026-06-01T00:00:00Z',
    ...p,
  };
}

function food(extra: Partial<MockProduct>): Partial<MockProduct> {
  return {
    category: 'CatFood',
    manufacturer: 'PurrCare Test Foods Ltd',
    analysis: { proteinPercent: 32, fatPercent: 16, fibrePercent: 3.5, moisturePercent: 9 },
    feedingGuide: FEEDING_GUIDE,
    storageInstructions:
      'Store in a cool, dry place. Reseal the bag and use within 6 weeks of opening.',
    isExpiryTracked: true,
    ...extra,
  };
}

const BRANDS: Array<{ brand: string; country: string; protein: string }> = [
  { brand: 'Whiskas', country: 'TH', protein: 'Fish' },
  { brand: 'Me-O', country: 'TH', protein: 'Tuna' },
  { brand: 'Sheba', country: 'TH', protein: 'Salmon' },
  { brand: 'Purina Pro Plan', country: 'US', protein: 'Chicken' },
  { brand: "Hill's Science Diet", country: 'US', protein: 'Chicken' },
  { brand: 'Animonda Carny', country: 'DE', protein: 'Beef' },
  { brand: 'Reflex', country: 'TR', protein: 'Chicken' },
  { brand: 'Pet Brand Cat', country: 'KR', protein: 'Salmon' },
  { brand: 'Kit Cat', country: 'TH', protein: 'Tuna' },
  { brand: 'Felix', country: 'FR', protein: 'Beef' },
  { brand: 'Monge', country: 'IN', protein: 'Lamb' },
  { brand: 'Brit Care', country: 'DE', protein: 'Turkey' },
];
const STAGES = ['Kitten', 'Adult', 'Senior'];
const TEXTURES = ['Dry', 'Wet'];

function seedGenerated(): MockProduct[] {
  const out: MockProduct[] = [];
  let n = 200;
  for (const [bi, b] of BRANDS.entries()) {
    for (const [si, stage] of STAGES.entries()) {
      n += 1;
      const texture = TEXTURES[(bi + si) % 2]!;
      const slug = `${b.brand.toLowerCase().replace(/[^a-z]+/g, '-')}-${stage.toLowerCase()}-${texture.toLowerCase()}-${n}`;
      const base = 450 + ((bi * 7 + si * 13) % 9) * 110;
      const stocked = (bi + si) % 4 !== 3;
      out.push(
        product({
          productId: uuid('a', n),
          slug,
          name: `${b.brand} ${stage} ${texture} Cat Food`,
          brand: b.brand,
          subcategory: texture,
          country: b.country,
          lifeStage: stage,
          protein: b.protein,
          tags: [
            texture.toLowerCase(),
            b.protein.toLowerCase(),
            si === 0 ? 'grain-free' : 'balanced',
          ],
          ingredientList: `${b.protein} meal, rice, ${b.protein === 'Chicken' ? 'chicken fat, ' : ''}vitamins and minerals`,
          ratingAverage: 3 + ((bi + si) % 3) * 0.7,
          ratingCount: 4 + bi * 3 + si,
          createdAt: `2026-0${1 + ((bi + si) % 8)}-15T00:00:00Z`,
          sold: 10 + bi * 17 + si * 5,
          ...food({}),
          skus: [
            sku(n * 10 + 1, `GEN-${n}-S`, '400 g', String(base) + '.00', stocked),
            sku(
              n * 10 + 2,
              `GEN-${n}-L`,
              '2 kg',
              String(base * 4 - 200) + '.00',
              stocked && si !== 1,
              {
                expiresOn: stocked ? '2027-03-01' : null,
              },
            ),
          ],
        }),
      );
    }
  }
  const others: Array<[string, string, string, string, string, string]> = [
    ['Litter', 'Tofu', 'Pretty Cat Tofu Litter', 'Pretty Cat', 'JP', '1100.00'],
    ['Litter', 'Clumping', 'Catsan Clumping Litter', 'Catsan', 'DE', '900.00'],
    ['Litter', 'Silica', 'Crystal Silica Litter', 'Cat Lover', 'DE', '1350.00'],
    ['LitterPotsAndAccessories', 'Scoops', 'Steel Litter Scoop', 'PurrCare', 'BD', '250.00'],
    ['LitterPotsAndAccessories', 'HoodedBoxes', 'Hooded Litter Box', 'Savic', 'FR', '3200.00'],
    ['BagsAndCarriers', 'Carriers', 'Airline Cat Carrier', 'Ferplast', 'IN', '4500.00'],
    ['BagsAndCarriers', 'Backpacks', 'Bubble Backpack', 'PetPal', 'KR', '5200.00'],
    ['HealthAndGrooming', '', 'Slicker Brush', 'Hertzko', 'US', '650.00'],
    ['HealthAndGrooming', '', 'Hairball Remedy Gel', 'Beaphar', 'FR', '780.00'],
    ['ToysAndFurniture', '', 'Feather Wand Toy', 'KONG', 'US', '420.00'],
    ['ToysAndFurniture', '', 'Sisal Scratching Post', 'Trixie', 'DE', '2400.00'],
    ['ToysAndFurniture', '', 'Catnip Mouse Set', 'Petstages', 'US', '320.00'],
  ];
  for (const [category, subcategory, name, brand, , price] of others) {
    n += 1;
    out.push(
      product({
        productId: uuid('a', n),
        slug: name.toLowerCase().replace(/[^a-z]+/g, '-'),
        name,
        brand,
        category,
        subcategory: subcategory || null,
        tags: [category.toLowerCase()],
        ratingAverage: 4.2,
        ratingCount: 9,
        sold: 30 + n,
        skus: [sku(n * 10 + 1, `GEN-${n}`, 'Standard', price, n % 5 !== 0)],
      }),
    );
  }
  return out;
}

export function seedProducts(): MockProduct[] {
  const hand: MockProduct[] = [
    product({
      productId: shopFixtureIds.multiVariantProductId,
      slug: shopFixtureIds.multiVariantSlug,
      name: 'Royal Canin Kitten',
      brand: 'Royal Canin',
      subcategory: 'Kitten',
      country: 'TH',
      lifeStage: 'Kitten',
      protein: 'Chicken',
      tags: ['kitten', 'growth', 'grain-free'],
      ingredientList:
        'Dehydrated poultry protein, rice, animal fats, wheat flour, vegetable protein isolate',
      ratingAverage: 4.5,
      ratingCount: 23,
      sold: 240,
      createdAt: '2026-09-01T00:00:00Z',
      ...food({}),
      related: [
        { slug: shopFixtureIds.thaiKittenSlug, kind: 'FrequentlyBoughtTogether', sortOrder: 1 },
        { slug: 'hairball-remedy-gel', kind: 'UpSell', sortOrder: 1 },
      ],
      skus: [
        sku(101, 'RC-K-400G', '400 g', '650.00', true),
        sku(102, 'RC-K-2KG', '2 kg', '2450.00', true, {
          expiresOn: '2026-11-12',
          scheduledPrice: { amount: '2600.00', effectiveFrom: '2026-12-01T00:00:00Z' },
        }),
        sku(103, 'RC-K-4KG', '4 kg', '4400.00', false),
      ],
    }),
    product({
      productId: shopFixtureIds.thaiKittenProductId,
      slug: shopFixtureIds.thaiKittenSlug,
      name: 'Whiskas Kitten Salmon',
      brand: 'Whiskas',
      subcategory: 'Kitten',
      country: 'TH',
      lifeStage: 'Kitten',
      protein: 'Salmon',
      tags: ['kitten', 'salmon', 'complete'],
      ingredientList: 'Salmon (14%), cereals, meat and animal derivatives, minerals',
      ratingAverage: 4,
      ratingCount: 8,
      sold: 180,
      createdAt: '2026-08-01T00:00:00Z',
      ...food({}),
      skus: [sku(201, 'WH-K-1KG', '1 kg', '780.00', true)],
      translations: [
        {
          locale: 'bn',
          name: 'হুইস্কাস কিটেন স্যামন',
          description: 'বাড়ন্ত বিড়ালছানার জন্য সম্পূর্ণ খাবার।',
          ingredientList: 'স্যামন (১৪%), শস্য, খনিজ',
          // feeding guide intentionally missing: falls back to en (US-SHP-14 AC2)
          feedingGuide: null,
        },
      ],
    }),
    product({
      productId: uuid('a', 3),
      slug: 'me-o-kitten-tuna',
      name: 'Me-O Kitten Tuna',
      brand: 'Me-O',
      subcategory: 'Kitten',
      country: 'TH',
      lifeStage: 'Kitten',
      protein: 'Tuna',
      tags: ['kitten', 'tuna'],
      ingredientList: 'Tuna, rice, fish oil, taurine',
      ratingAverage: 3.8,
      ratingCount: 12,
      sold: 150,
      createdAt: '2026-07-10T00:00:00Z',
      ...food({}),
      skus: [sku(301, 'ME-K-1KG', '1 kg', '690.00', true)],
    }),
    product({
      productId: uuid('a', 4),
      slug: 'purina-pro-plan-adult-chicken',
      name: 'Purina Pro Plan Adult Chicken',
      brand: 'Purina Pro Plan',
      subcategory: 'Adult',
      country: 'US',
      lifeStage: 'Adult',
      protein: 'Chicken',
      tags: ['adult', 'chicken', 'sensitive'],
      ingredientList: 'Chicken, rice, corn gluten meal, chicken fat',
      ratingAverage: 4.7,
      ratingCount: 31,
      sold: 300,
      ...food({}),
      skus: [
        sku(401, 'PP-AD-1KG', '1 kg', '1800.00', true),
        sku(402, 'PP-AD-3KG', '3 kg', '4900.00', true),
      ],
    }),
    product({
      productId: shopFixtureIds.rxNoAuthProductId,
      slug: shopFixtureIds.rxNoAuthSlug,
      name: "Hill's c/d Urinary Care",
      brand: "Hill's Prescription Diet",
      subcategory: 'Therapeutic',
      country: 'US',
      lifeStage: 'Adult',
      protein: 'Chicken',
      prescriptionRequired: true,
      tags: ['urinary', 'prescription'],
      ingredientList: 'Rice, chicken, pork fat, dried beet pulp',
      ratingAverage: 4.4,
      ratingCount: 6,
      sold: 90,
      ...food({}),
      skus: [sku(501, 'HL-CD-1_5KG', '1.5 kg', '3800.00', true)],
    }),
    product({
      productId: shopFixtureIds.rxEligibleProductId,
      slug: shopFixtureIds.rxEligibleSlug,
      name: "Hill's k/d Renal Care",
      brand: "Hill's Prescription Diet",
      subcategory: 'Therapeutic',
      country: 'US',
      lifeStage: 'Senior',
      protein: 'Chicken',
      prescriptionRequired: true,
      tags: ['renal', 'prescription'],
      ingredientList: 'Rice, chicken fat, pork liver, flaxseed',
      ratingAverage: 4.1,
      ratingCount: 3,
      sold: 40,
      ...food({}),
      skus: [sku(601, 'HL-KD-1_5KG', '1.5 kg', '4100.00', true)],
    }),
    product({
      productId: shopFixtureIds.kitProductId,
      slug: shopFixtureIds.kitSlug,
      name: 'Kitten Starter Kit',
      brand: 'PurrCare',
      kind: 'Bundle',
      category: 'CatFood',
      subcategory: 'Kitten',
      lifeStage: 'Kitten',
      tags: ['kit', 'kitten'],
      ratingAverage: null,
      ratingCount: 0,
      sold: 20,
      skus: [sku(701, 'KIT-KITTEN', 'Starter kit', '1500.00', true)],
      bundle: [
        {
          productSlug: shopFixtureIds.multiVariantSlug,
          skuId: shopFixtureIds.skuKitten400,
          quantity: 1,
        },
        { productSlug: 'catnip-mouse-set', skuId: uuid('b', 9999), quantity: 2 },
      ],
    }),
    product({
      productId: uuid('a', 8),
      slug: shopFixtureIds.noConsentSlug,
      name: 'Reflex Adult Chicken',
      brand: 'Reflex',
      subcategory: 'Adult',
      country: 'TR',
      lifeStage: 'Adult',
      protein: 'Chicken',
      tags: ['adult', 'chicken'],
      ingredientList: 'Chicken, rice, maize, chicken fat',
      ratingAverage: 3.9,
      ratingCount: 5,
      sold: 70,
      ...food({}),
      skus: [sku(801, 'RF-AD-1KG', '1 kg', '1250.00', false)],
    }),
    product({
      productId: uuid('a', 9),
      slug: 'animonda-carny-adult-beef',
      name: 'Animonda Carny Adult Beef',
      brand: 'Animonda Carny',
      subcategory: 'Wet',
      country: 'DE',
      lifeStage: 'Adult',
      protein: 'Beef',
      tags: ['wet', 'beef', 'grain-free'],
      ingredientList: 'Beef, beef heart, minerals',
      ratingAverage: 4.6,
      ratingCount: 17,
      sold: 120,
      ...food({}),
      skus: [sku(901, 'AN-CA-400G', '400 g', '520.00', true)],
    }),
    product({
      productId: uuid('a', 10),
      slug: 'pet-brand-cat-salmon',
      name: 'Pet Brand Cat Salmon',
      brand: 'Pet Brand Cat',
      subcategory: 'Dry',
      country: 'KR',
      lifeStage: 'AllLifeStages',
      protein: 'Salmon',
      tags: ['salmon', 'all-life-stages'],
      ingredientList: 'Salmon, sweet potato, flaxseed',
      ratingAverage: 4.3,
      ratingCount: 10,
      sold: 60,
      ...food({}),
      skus: [sku(1001, 'PB-SA-1KG', '1 kg', '1650.00', true)],
    }),
    product({
      productId: shopFixtureIds.draftProductId,
      slug: shopFixtureIds.draftSlug,
      name: 'Hairball Gel (draft)',
      brand: 'Beaphar',
      category: 'HealthAndGrooming',
      subcategory: null,
      status: 'Draft',
      firstPublishedOn: null,
      skus: [sku(9001, 'DRAFT-1', 'Standard', null, false)],
    }),
    product({
      productId: shopFixtureIds.unmetChecklistProductId,
      slug: 'unmet-checklist-food',
      name: 'Unfinished Kitten Food',
      brand: 'Pet Brand Cat',
      status: 'Draft',
      firstPublishedOn: null,
      skus: [sku(9101, 'UNMET-1', '1 kg', null, false)],
    }),
  ];
  // Make the kit's second component a real out-of-stock product (the catnip set).
  const generated = seedGenerated();
  const catnip = generated.find((g) => g.slug === 'catnip-mouse-set');
  if (catnip) {
    catnip.skus[0]!.skuId = uuid('b', 9999);
    catnip.skus[0]!.inStock = false;
  }
  return [...hand, ...generated];
}

export interface MockOrigin {
  code: string;
  sourcingNotes: string | null;
  isFeatured: boolean;
  sortOrder: number;
}

export function seedOrigins(): MockOrigin[] {
  return [
    {
      code: 'TH',
      sourcingNotes:
        'Our Thai partners cook in small batches near **Bangkok**. Every lot is tested before it ships to Dhaka, and we visit the plant each year to check hygiene and storage.',
      isFeatured: true,
      sortOrder: 1,
    },
    {
      code: 'DE',
      sourcingNotes: 'Wet foods from a family-run German cannery, cooked without added sugar.',
      isFeatured: true,
      sortOrder: 2,
    },
    {
      code: 'US',
      sourcingNotes: 'Veterinary and performance diets from established US brands.',
      isFeatured: false,
      sortOrder: 3,
    },
    { code: 'TR', sourcingNotes: null, isFeatured: false, sortOrder: 4 },
    { code: 'KR', sourcingNotes: null, isFeatured: false, sortOrder: 5 },
  ];
}

export interface MockReview {
  reviewId: string;
  productId: string;
  slug: string;
  productName: string;
  skuId: string;
  skuLabel: string;
  rating: number;
  title: string | null;
  body: string;
  status: 'PendingModeration' | 'Published' | 'Rejected' | 'Hidden';
  submittedOn: string;
  authorDisplayName: string;
  mine: boolean;
}

export function seedReviews(): MockReview[] {
  const base = {
    productId: shopFixtureIds.multiVariantProductId,
    slug: shopFixtureIds.multiVariantSlug,
    productName: 'Royal Canin Kitten',
    skuId: shopFixtureIds.skuKitten2kg,
    skuLabel: '2 kg',
  };
  return [
    {
      ...base,
      reviewId: shopFixtureIds.publishedReviewId,
      rating: 5,
      title: 'My kitten loves it',
      body: 'Eats every bite and the coat is shiny. Would buy again.',
      status: 'Published',
      submittedOn: '2026-10-03T09:00:00Z',
      authorDisplayName: 'Rahim K.',
      mine: false,
    },
    {
      ...base,
      reviewId: uuid('d', 5),
      rating: 4,
      title: 'Good, but pricey',
      body: 'Great quality. <b>Not</b> cheap though **really**.',
      status: 'Published',
      submittedOn: '2026-09-20T09:00:00Z',
      authorDisplayName: 'A verified buyer',
      mine: false,
    },
    {
      ...base,
      productId: shopFixtureIds.thaiKittenProductId,
      slug: shopFixtureIds.thaiKittenSlug,
      productName: 'Whiskas Kitten Salmon',
      skuId: shopFixtureIds.skuWhiskasKitten,
      skuLabel: '1 kg',
      reviewId: shopFixtureIds.pendingReviewId,
      rating: 4,
      title: 'Nice salmon smell',
      body: 'My cat came running when I opened the bag.',
      status: 'PendingModeration',
      submittedOn: '2026-10-02T09:00:00Z',
      authorDisplayName: 'You',
      mine: true,
    },
    {
      ...base,
      productId: uuid('a', 4),
      slug: 'purina-pro-plan-adult-chicken',
      productName: 'Purina Pro Plan Adult Chicken',
      skuId: uuid('b', 401),
      skuLabel: '1 kg',
      reviewId: shopFixtureIds.rejectedReviewId,
      rating: 1,
      title: null,
      body: 'Not for us.',
      status: 'Rejected',
      submittedOn: '2026-09-10T09:00:00Z',
      authorDisplayName: 'You',
      mine: true,
    },
    {
      ...base,
      productId: uuid('a', 9),
      slug: 'animonda-carny-adult-beef',
      productName: 'Animonda Carny Adult Beef',
      skuId: uuid('b', 901),
      skuLabel: '400 g',
      reviewId: shopFixtureIds.hiddenReviewId,
      rating: 3,
      title: 'Okay',
      body: 'Fine for the price.',
      status: 'Hidden',
      submittedOn: '2026-08-10T09:00:00Z',
      authorDisplayName: 'You',
      mine: true,
    },
    {
      ...base,
      reviewId: shopFixtureIds.moderationPendingReviewId,
      rating: 2,
      title: 'Too small bag',
      body: 'Wish the bag was bigger.',
      status: 'PendingModeration',
      submittedOn: '2026-10-04T05:00:00Z',
      authorDisplayName: 'Nadia S.',
      mine: false,
    },
  ];
}

export function badRequest(
  propertyName: string,
  errorCode: string,
  errorMessage: string,
  resourceName = 'Product',
) {
  return HttpResponse.json(
    { errors: [{ propertyName, errorCode, errorMessage, resourceName }], isValid: false },
    { status: 400 },
  );
}
