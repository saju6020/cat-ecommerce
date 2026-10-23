'use client';

import * as React from 'react';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { useQuickAdd } from '@/components/catalogue/ProductCard';
import { HearthEyebrow } from '@/components/hearth/HearthBand';
import { ProductPackArt } from '@/components/hearth/ProductPackArt';
import { formatPriceRange } from '@/lib/format/shop';
import { availabilityText } from '@/lib/shop/labels';
import { productHref } from '@/lib/shop/helpers';
import { cn } from '@/lib/utils';
import type { CatalogueItem } from '@/lib/api/shop-catalogue';

const ROW_TONES = ['warm', 'sunken', 'teal'] as const;
const TILE_BG = {
  warm: 'bg-warm-soft',
  sunken: 'bg-surface-sunken',
  teal: 'bg-accent-tint',
} as const;

function Row({
  item,
  index,
  lang,
  onMessage,
}: {
  item: CatalogueItem;
  index: number;
  lang?: string;
  onMessage: (text: string, kind: 'ok' | 'problem') => void;
}) {
  const href = productHref(item.slug, { lang });
  const quickAdd = useQuickAdd(item, href, onMessage);
  const tone = ROW_TONES[index % ROW_TONES.length]!;
  const outOfStock = item.availability === 'OutOfStock';
  return (
    <li className="flex items-center gap-3 rounded-2xl bg-surface p-3">
      <span
        aria-hidden="true"
        className={cn('grid size-[52px] flex-none place-items-center rounded-xl', TILE_BG[tone])}
      >
        <ProductPackArt
          tone={tone}
          className="size-full rounded-none bg-transparent [&>svg]:h-[62%]"
        />
      </span>
      <span className="flex min-w-0 flex-1 flex-col gap-0.5 text-sm">
        <Link
          href={href}
          className="font-semibold text-ink no-underline hover:text-accent hover:underline"
        >
          {item.name}
        </Link>
        <small className="text-[13px] text-ink-muted">
          {formatPriceRange(item.fromPrice, item.priceVaries)} ·{' '}
          {availabilityText(item.availability)}
        </small>
      </span>
      {outOfStock ? (
        <Link
          href={`${href}#stock-alert`}
          aria-label={`Tell me when ${item.name} is back`}
          className="inline-flex min-h-11 flex-none items-center rounded-pill border border-border-strong bg-surface px-3 text-[13px] font-semibold text-accent-ink no-underline hover:bg-accent-tint"
        >
          Alert me
        </Link>
      ) : (
        <button
          type="button"
          aria-busy={quickAdd.busy || undefined}
          aria-label={
            quickAdd.chooseOnPage ? `Add ${item.name}: choose options` : `Add ${item.name} to cart`
          }
          onClick={() => void quickAdd.run()}
          className="grid size-11 flex-none place-items-center rounded-pill border border-border-strong bg-surface p-0 text-accent hover:bg-accent-tint focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <Plus size={20} aria-hidden="true" />
        </button>
      )}
    </li>
  );
}

/**
 * HO-SHP-002 §11.3, US-SHP-08 — "Often bought together": the server's frequently-bought-together
 * list as a warm card beside the product details, each row with a 44px *Add* (+) button that follows
 * the same rules as a catalogue card. Renders nothing when the list is empty.
 */
export function OftenBoughtTogether({ items, lang }: { items: CatalogueItem[]; lang?: string }) {
  const [note, setNote] = React.useState<{ text: string; kind: 'ok' | 'problem' } | null>(null);
  if (items.length === 0) return null;
  return (
    <section
      aria-labelledby="fbt-title"
      className="flex flex-col gap-3 rounded-[24px] bg-warm-soft p-7"
    >
      <h2 id="fbt-title" className="m-0">
        <HearthEyebrow className="tracking-[0.08em]">Often bought together</HearthEyebrow>
      </h2>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {items.slice(0, 4).map((item, index) => (
          <Row
            key={item.productId}
            item={item}
            index={index}
            lang={lang}
            onMessage={(text, kind) => setNote({ text, kind })}
          />
        ))}
      </ul>
      <p
        role={note?.kind === 'problem' ? 'alert' : 'status'}
        className={cn(
          'm-0 text-sm font-semibold empty:hidden',
          note?.kind === 'problem' ? 'text-danger-ink' : 'text-warm-ink',
        )}
      >
        {note?.text}
        {note?.kind === 'ok' && (
          <>
            {' '}
            <Link href="/cart" className="underline">
              View cart
            </Link>
          </>
        )}
      </p>
    </section>
  );
}
