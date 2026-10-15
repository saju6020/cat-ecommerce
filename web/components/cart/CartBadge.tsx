'use client';

import Link from 'next/link';
import { ShoppingBasket } from 'lucide-react';
import { useCart } from '@/lib/hooks/useCart';

/**
 * `components/cart/CartBadge.tsx` — HO-ORD-002 §3.3. The cart link for a storefront or portal header:
 * the item count from `['cart']`, **hidden at 0**, linking to `/cart`. It also mounts `useCart`, so a
 * signed-in customer with a waiting guest cart has it merged wherever they land first. A failed read
 * shows the link without a count rather than an error in the header. Below 640 px the word "Cart" is dropped (icon and count
 * stay; the accessible name is unchanged) because the portal header, which also holds the inbox, the name and Sign out, would
 * otherwise be wider than a 360 px screen once the cart has an item.
 */
export function CartBadge() {
  const { data } = useCart();
  const count = data?.itemCount ?? 0;
  return (
    <Link
      href="/cart"
      className="inline-flex items-center gap-1.5 text-sm font-semibold text-ink no-underline"
      aria-label={count > 0 ? `Cart, ${count} ${count === 1 ? 'item' : 'items'}` : 'Cart'}
    >
      <ShoppingBasket size={16} aria-hidden="true" />
      <span aria-hidden="true" className="hidden sm:inline">Cart</span>
      {count > 0 && (
        <span
          data-testid="cart-count"
          aria-hidden="true"
          className="rounded-full bg-accent px-1.5 text-[11px] font-bold leading-5 text-white"
        >
          {count}
        </span>
      )}
    </Link>
  );
}
