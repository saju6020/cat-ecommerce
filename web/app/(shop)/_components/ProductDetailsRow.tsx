'use client';

import * as React from 'react';
import { OftenBoughtTogether } from '@/components/recommendations/OftenBoughtTogether';
import { useRecommendations } from '@/lib/hooks/useShopCatalogue';
import { cn } from '@/lib/utils';

/**
 * The product details card with "Often bought together" beside it. The list comes from the
 * existing recommendations read (the same query the blocks below use); when it is empty or fails,
 * the card is not drawn and the details take the full width.
 */
export function ProductDetailsRow({
  slug,
  lang,
  children,
}: {
  slug: string;
  lang: string;
  children: React.ReactNode;
}) {
  const { data } = useRecommendations(slug, lang);
  const items = data?.frequentlyBoughtTogether ?? [];
  return (
    <div
      className={cn(
        'grid items-start gap-8',
        items.length > 0 && 'lg:grid-cols-[minmax(0,1.65fr)_minmax(0,1fr)]',
      )}
    >
      {children}
      <OftenBoughtTogether items={items} lang={lang || undefined} />
    </div>
  );
}
