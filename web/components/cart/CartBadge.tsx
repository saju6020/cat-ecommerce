'use client';

import Link from 'next/link';
import { ShoppingBasket, ShoppingCart } from 'lucide-react';
import { useCart } from '@/lib/hooks/useCart';

/**
 * `components/cart/CartBadge.tsx` — HO-ORD-002 §3.3. The cart link for a storefront or portal header:
 * the item count from `['cart']`, **hidden at 0**, linking to `/cart`. It also mounts `useCart`, so a
 * signed-in customer with a waiting guest cart has it merged wherever they land first. A failed read
 * shows the link without a count rather than an error in the header. Below 640 px the word "Cart" is dropped (icon and count
 * stay; the accessible name is unchanged) because the portal header, which also holds the inbox, the name and Sign out, would
 * otherwise be wider than a 360 px screen once the cart has an item.
 */
/** `icon` (the customer account header, owner's design 2026-10-17): a round button with an orange count. */
export function CartBadge({ appearance = 'pill' }: { appearance?: 'pill' | 'icon' } = {}) {
  const { data } = useCart();
  const count = data?.itemCount ?? 0;
  if (appearance === 'icon') {
    return (
      <Link
        href="/cart"
        aria-label={count > 0 ? `Cart, ${count} ${count === 1 ? 'item' : 'items'}` : 'Cart'}
        className="relative grid size-[46px] place-items-center rounded-full border border-border bg-surface text-ink no-underline hover:border-accent"
      >
        <ShoppingCart size={20} aria-hidden="true" />
        {count > 0 && (
          <span
            data-testid="cart-count"
            aria-hidden="true"
            className="absolute -right-1.5 -top-1.5 grid h-[22px] min-w-[22px] place-items-center rounded-full bg-warm px-1.5 text-xs font-bold text-ink"
          >
            {count}
          </span>
        )}
      </Link>
    );
  }
  return (
    <Link
      href="/cart"
      className="inline-flex min-h-11 items-center gap-2 rounded-pill bg-warm-soft px-4 text-sm font-bold text-warm-ink no-underline"
      aria-label={count > 0 ? `Cart, ${count} ${count === 1 ? 'item' : 'items'}` : 'Cart'}
    >
      <ShoppingBasket size={18} aria-hidden="true" />
      <span aria-hidden="true" className="hidden sm:inline">
        Cart
      </span>
      {count > 0 && (
        <span
          data-testid="cart-count"
          aria-hidden="true"
          className="inline-grid h-[22px] min-w-[22px] place-items-center rounded-pill bg-accent px-1.5 text-xs font-bold text-white"
        >
          {count}
        </span>
      )}
    </Link>
  );
}
