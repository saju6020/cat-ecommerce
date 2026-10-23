import { ProductCard } from './ProductCard';
import { cn } from '@/lib/utils';
import type { CatalogueItem } from '@/lib/api/shop-catalogue';

/**
 * 2 columns at 360, 3 from 768; 3 at 1280 beside the filters (so the price and *Add* fit), or 4
 * when the grid has the full 1160px column (`wide`, the storefront home). UI-1, §7.
 */
export function ProductGrid({
  items,
  lang,
  headingLevel,
  label,
  wide,
}: {
  items: CatalogueItem[];
  lang?: string;
  headingLevel?: 'h2' | 'h3';
  label?: string;
  wide?: boolean;
}) {
  return (
    <ul
      aria-label={label}
      className={cn(
        'grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-3 md:gap-5',
        wide && 'lg:grid-cols-4',
      )}
    >
      {items.map((item) => (
        <li key={`${item.productId}-${item.skuId ?? ''}`} className="min-w-0">
          <ProductCard item={item} lang={lang} headingLevel={headingLevel} />
        </li>
      ))}
    </ul>
  );
}
