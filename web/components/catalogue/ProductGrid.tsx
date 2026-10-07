import { ProductCard } from './ProductCard';
import type { CatalogueItem } from '@/lib/api/shop-catalogue';

/** 2 columns at 360, 3 at 768, 4 at 1024+ (UI-1, §7). */
export function ProductGrid({
  items,
  lang,
  headingLevel,
  label,
}: {
  items: CatalogueItem[];
  lang?: string;
  headingLevel?: 'h2' | 'h3';
  label?: string;
}) {
  return (
    <ul
      aria-label={label}
      className="grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-3 lg:grid-cols-4"
    >
      {items.map((item) => (
        <li key={item.productId} className="min-w-0">
          <ProductCard item={item} lang={lang} headingLevel={headingLevel} />
        </li>
      ))}
    </ul>
  );
}
