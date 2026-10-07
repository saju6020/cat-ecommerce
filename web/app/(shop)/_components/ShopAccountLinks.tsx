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

  if (isPending) return <span className="h-5 w-20" aria-hidden="true" />;
  if (!session) {
    return (
      <Link
        href={`/sign-in?next=${encodeURIComponent(here)}`}
        className="inline-flex items-center gap-1.5 text-sm font-semibold text-accent-ink no-underline"
      >
        <User size={16} aria-hidden="true" />
        Sign in
      </Link>
    );
  }
  return (
    <div className="flex items-center gap-4 text-sm font-semibold">
      <Link
        href="/portal/wishlist"
        className="inline-flex items-center gap-1.5 text-ink no-underline"
      >
        <Heart size={16} aria-hidden="true" />
        Wishlist
      </Link>
      <Link href="/portal/cats" className="inline-flex items-center gap-1.5 text-ink no-underline">
        <User size={16} aria-hidden="true" />
        {firstName(session.fullName) || 'Your account'}
      </Link>
    </div>
  );
}
