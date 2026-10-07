import type { CatalogueFilters, CatalogueItem, CategoryNode } from '@/lib/api/shop-catalogue';

export function makeItem(overrides: Partial<CatalogueItem> = {}): CatalogueItem {
  return {
    productId: 'p-1',
    slug: 'royal-canin-kitten',
    name: 'Royal Canin Kitten',
    brand: 'Royal Canin',
    category: 'CatFood',
    subcategory: 'Kitten',
    kind: 'Standard',
    countryOfOrigin: { code: 'TH', name: 'Thailand' },
    lifeStage: 'Kitten',
    prescriptionRequired: false,
    fromPrice: { amount: '650.00', currencyCode: 'BDT' },
    priceVaries: false,
    availability: 'InStock',
    rating: { average: 4.5, count: 23 },
    primaryImage: { url: null, altText: 'Royal Canin Kitten bag', isPrimary: true },
    ...overrides,
  };
}

export const FACETS: CatalogueFilters = {
  brands: [
    { name: 'Royal Canin', count: 4 },
    { name: 'Whiskas', count: 6 },
  ],
  origins: [
    { code: 'TH', name: 'Thailand', count: 9 },
    { code: 'JP', name: 'Japan', count: 2 },
  ],
  lifeStages: [
    { value: 'Kitten', count: 5 },
    { value: 'Adult', count: 7 },
  ],
  proteinSources: [{ value: 'Salmon', count: 3 }],
  price: { min: 250, max: 5200 },
};

export const CATEGORIES: CategoryNode[] = [
  {
    value: 'CatFood',
    productCount: 20,
    subcategories: [
      { value: 'Dry', productCount: 10 },
      { value: 'Kitten', productCount: 5 },
    ],
  },
  { value: 'Litter', productCount: 4, subcategories: [] },
];
