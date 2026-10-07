/**
 * `lib/shop/labels.ts` — HO-SHP-002 §11. Display names for the API's enumeration values (which come
 * by name, e.g. `CatFood`, `AllLifeStages`). Labels are English-only chrome (C-8); product CONTENT is
 * what FR-SHP-15 translates. Unknown values fall back to a spaced version of the name.
 */

const CATEGORY_LABELS: Record<string, string> = {
  CatFood: 'Cat food',
  Litter: 'Litter',
  LitterPotsAndAccessories: 'Litter pots & accessories',
  BagsAndCarriers: 'Bags & carriers',
  HealthAndGrooming: 'Health & grooming',
  ToysAndFurniture: 'Toys & furniture',
};

const SUBCATEGORY_LABELS: Record<string, string> = {
  HoodedBoxes: 'Hooded boxes',
  TravelBags: 'Travel bags',
  StorageBags: 'Storage bags',
};

const LIFE_STAGE_LABELS: Record<string, string> = {
  Kitten: 'Kitten',
  Adult: 'Adult',
  Senior: 'Senior',
  AllLifeStages: 'All life stages',
};

const PROTEIN_LABELS: Record<string, string> = { PlantBased: 'Plant-based' };

function spaced(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export const categoryLabel = (value: string) => CATEGORY_LABELS[value] ?? spaced(value);
export const subcategoryLabel = (value: string) => SUBCATEGORY_LABELS[value] ?? spaced(value);
export const lifeStageLabel = (value: string) => LIFE_STAGE_LABELS[value] ?? spaced(value);
export const proteinLabel = (value: string) => PROTEIN_LABELS[value] ?? spaced(value);

/** "Recommended for kittens / adult cats / senior cats" (US-SHP-08). */
export function lifeStageAudience(stages: string[]): string {
  const names = stages.map((stage) =>
    stage === 'Kitten'
      ? 'kittens'
      : stage === 'Senior'
        ? 'senior cats'
        : stage === 'Adult'
          ? 'adult cats'
          : 'cats of every age',
  );
  return names.length > 0 ? names.join(' and ') : 'cats';
}

export const SORT_LABELS = {
  relevance: 'Relevance',
  newest: 'Newest',
  price_asc: 'Price: low to high',
  price_desc: 'Price: high to low',
  popularity: 'Popularity',
  rating: 'Rating',
} as const;

/** Availability is ALWAYS text (plus an icon in the component), never colour alone (UI-6). */
export function availabilityText(availability: 'InStock' | 'OutOfStock'): string {
  return availability === 'InStock' ? 'In stock' : 'Out of stock';
}
