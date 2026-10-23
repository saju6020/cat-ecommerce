'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';
import { Heart, User } from 'lucide-react';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { firstName } from '@/lib/shop/helpers';

/**
 * HO-SHP-002 §11.1 — the storefront reads `['session','customer']` to show *Sign in* or the
 * customer's first name (and a wishlist link). The `(shop)` group has NO guard (ADR-FE-029): a guest
 * browses freely, and *Sign in* returns here through the allow-listed `?next=` (ADR-FE-038).
 */
export function ShopAccountLinks() {
  const { data: session, isPending } = useCustomerSession();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const query = searchParams.toString();
  const here = `${pathname}${query ? `?${query}` : ''}`;

  if (isPending) return <span className="h-11 w-20" aria-hidden="true" />;
  if (!session) {
    return (
      <Link
        href={`/sign-in?next=${encodeURIComponent(here)}`}
        className="inline-flex min-h-11 items-center gap-2 text-sm font-semibold text-accent-ink no-underline"
      >
        <User size={18} aria-hidden="true" />
        {/* Icon only on a phone (the pinned header stays one row); the name stays for screen readers. */}
        <span className="max-sm:sr-only">Sign in</span>
      </Link>
    );
  }
  return (
    <div className="flex items-center gap-3 text-sm font-semibold sm:gap-4">
      <Link
        href="/portal/wishlist"
        className="inline-flex min-h-11 items-center gap-2 text-accent-ink no-underline"
      >
        <Heart size={18} aria-hidden="true" />
        <span className="max-sm:sr-only">Wishlist</span>
      </Link>
      {/* The account opens on My orders (owner, 2026-10-17). */}
      <Link
        href="/portal/orders"
        className="inline-flex min-h-11 items-center gap-2 text-accent-ink no-underline"
      >
        <User size={18} aria-hidden="true" />
        <span className="max-sm:sr-only">{firstName(session.fullName) || 'Your account'}</span>
      </Link>
    </div>
  );
}
