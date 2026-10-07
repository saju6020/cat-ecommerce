import Link from 'next/link';
import { Package } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { ProductImage } from '@/components/product/ProductImage';
import {
  AvailabilityText,
  OriginText,
  PrescriptionBadge,
  RatingText,
} from '@/components/product/StatusText';
import { formatPriceRange } from '@/lib/format/shop';
import type { CatalogueItem } from '@/lib/api/shop-catalogue';

export interface ProductCardProps {
  item: CatalogueItem;
  /** Content language carried onto the product link (ADR-FE-037). */
  lang?: string;
  headingLevel?: 'h2' | 'h3';
}

/**
 * HO-SHP-002 §11.2 `ProductCard` — an `<article>` whose name is the heading link. Everything the
 * shopper needs to decide is text: price ("From" when variants differ), rating, availability, the
 * prescription badge (text + icon) and the kit badge. Names and brands are PLAIN TEXT.
 */
export function ProductCard({ item, lang, headingLevel = 'h2' }: ProductCardProps) {
  const Heading = headingLevel;
  const href = `/shop/products/${encodeURIComponent(item.slug)}${lang ? `?lang=${encodeURIComponent(lang)}` : ''}`;
  return (
    <article className="flex h-full flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-3">
      <ProductImage
        image={item.primaryImage}
        fallbackAlt={`${item.name} — photo coming soon`}
        size={240}
      />
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{item.brand}</p>
      <Heading className="text-[15px] font-semibold leading-snug text-ink">
        <Link href={href} className="text-ink no-underline hover:underline">
          {item.name}
        </Link>
      </Heading>
      {item.countryOfOrigin && <OriginText country={item.countryOfOrigin} />}
      <p className="text-base font-bold text-ink">
        {formatPriceRange(item.fromPrice, item.priceVaries)}
      </p>
      <RatingText rating={item.rating} />
      <div className="mt-auto flex flex-wrap items-center gap-2 pt-1">
        <AvailabilityText availability={item.availability} />
        {item.prescriptionRequired && <PrescriptionBadge />}
        {item.kind === 'Bundle' && (
          <Badge variant="neutral" icon={<Package size={12} aria-hidden="true" />}>
            Kit
          </Badge>
        )}
      </div>
    </article>
  );
}
