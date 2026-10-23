import type { PackTone } from '@/components/hearth/ProductPackArt';

/**
 * Which tint a product's placeholder pack gets, by its catalogue category, so a listing is not one
 * flat colour: food is warm, litter and accessories sunken, health and grooming teal, and bags /
 * carriers / toys alternate warm and sunken. Decorative only; an unknown category falls back to
 * sunken. A real photo always wins over the placeholder at the call site.
 */
const TONE_BY_CATEGORY: Record<string, PackTone> = {
  CatFood: 'warm',
  Litter: 'sunken',
  LitterPotsAndAccessories: 'sunken',
  HealthAndGrooming: 'teal',
  BagsAndCarriers: 'warm',
  ToysAndFurniture: 'sunken',
};

export function packToneForCategory(category: string | null | undefined): PackTone {
  return (category && TONE_BY_CATEGORY[category]) || 'sunken';
}

/** The matching background for a card image area behind the (transparent) pack art. */
export const PACK_BG: Record<PackTone, string> = {
  warm: 'bg-warm-soft',
  teal: 'bg-accent-tint',
  sunken: 'bg-surface-sunken',
};
