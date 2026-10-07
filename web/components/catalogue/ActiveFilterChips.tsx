import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { toggleValue, type CatalogueParams } from '@/lib/shop/catalogueParams';
import { categoryLabel, lifeStageLabel, proteinLabel, subcategoryLabel } from '@/lib/shop/labels';
import type { CatalogueFilters } from '@/lib/api/shop-catalogue';

export interface ActiveFilterChipsProps {
  value: CatalogueParams;
  /** Origin names for the chip text — from the same facets the filter panel uses. */
  origins: CatalogueFilters['origins'];
  currencyCode: string;
  /** Fixed filters (an origin page's country) are never chips. */
  fixedKeys?: ReadonlyArray<keyof CatalogueParams>;
  onChange: (patch: Partial<CatalogueParams>) => void;
  onClearAll: () => void;
}

interface Chip {
  key: string;
  label: string;
  patch: Partial<CatalogueParams>;
}

function chipsFor(
  value: CatalogueParams,
  origins: CatalogueFilters['origins'],
  currencyCode: string,
  fixed: ReadonlyArray<keyof CatalogueParams>,
): Chip[] {
  const chips: Chip[] = [];
  if (value.category) {
    chips.push({
      key: 'category',
      label: categoryLabel(value.category),
      patch: { category: '', subcategory: '' },
    });
  }
  if (value.subcategory) {
    chips.push({
      key: 'subcategory',
      label: subcategoryLabel(value.subcategory),
      patch: { subcategory: '' },
    });
  }
  if (!fixed.includes('origin')) {
    for (const code of value.origin) {
      const name = origins.find((o) => o.code === code)?.name ?? code;
      chips.push({
        key: `origin-${code}`,
        label: name,
        patch: { origin: toggleValue(value.origin, code) },
      });
    }
  }
  for (const brand of value.brand) {
    chips.push({
      key: `brand-${brand}`,
      label: brand,
      patch: { brand: toggleValue(value.brand, brand) },
    });
  }
  for (const stage of value.lifeStage) {
    chips.push({
      key: `stage-${stage}`,
      label: lifeStageLabel(stage),
      patch: { lifeStage: toggleValue(value.lifeStage, stage) },
    });
  }
  for (const protein of value.protein) {
    chips.push({
      key: `protein-${protein}`,
      label: proteinLabel(protein),
      patch: { protein: toggleValue(value.protein, protein) },
    });
  }
  if (value.minPrice || value.maxPrice) {
    const range =
      value.minPrice && value.maxPrice
        ? `${value.minPrice}–${value.maxPrice}`
        : value.minPrice
          ? `from ${value.minPrice}`
          : `up to ${value.maxPrice}`;
    chips.push({
      key: 'price',
      label: `Price ${range} ${currencyCode}`,
      patch: { minPrice: '', maxPrice: '' },
    });
  }
  if (value.inStock)
    chips.push({ key: 'instock', label: 'In stock only', patch: { inStock: false } });
  if (value.minRating !== null) {
    chips.push({ key: 'rating', label: `${value.minRating} and up`, patch: { minRating: null } });
  }
  return chips;
}

/** HO-SHP-002 §11.2 — active filters as removable chips; *Clear all* keeps the search text. */
export function ActiveFilterChips({
  value,
  origins,
  currencyCode,
  fixedKeys = [],
  onChange,
  onClearAll,
}: ActiveFilterChipsProps) {
  const chips = chipsFor(value, origins, currencyCode, fixedKeys);
  if (chips.length === 0) return null;
  return (
    <ul aria-label="Active filters" className="m-0 flex list-none flex-wrap items-center gap-2 p-0">
      {chips.map((chip) => (
        <li key={chip.key}>
          <button
            type="button"
            onClick={() => onChange(chip.patch)}
            aria-label={`Remove filter: ${chip.label}`}
            className="inline-flex items-center gap-1.5 rounded-full border border-border-strong bg-surface px-3 py-1 text-sm text-ink hover:bg-surface-sunken"
          >
            {chip.label}
            <X size={12} aria-hidden="true" />
          </button>
        </li>
      ))}
      <li>
        <Button variant="ghost" size="sm" onClick={onClearAll}>
          Clear all
        </Button>
      </li>
    </ul>
  );
}
