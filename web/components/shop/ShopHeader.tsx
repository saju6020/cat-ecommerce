import * as React from 'react';
import { Suspense } from 'react';
import Link from 'next/link';
import { Menu } from 'lucide-react';
import { CartBadge } from '@/components/cart/CartBadge';
import { HearthLogo } from '@/components/hearth/HearthLogo';
import { CategoryChips, CurrentCategoryChips } from './CategoryChips';
import { SearchBox, SearchForm } from './SearchBox';
import type { CategoryNode } from '@/lib/api/shop-catalogue';

export interface ShopHeaderProps {
  categories: CategoryNode[];
  /** Sign in / the customer's name / wishlist (a client island that reads the session). */
  accountSlot: React.ReactNode;
  /** The language picker (only rendered by the layout when more than one locale is enabled). */
  languageSlot?: React.ReactNode;
}

function CategoryLinks({ categories }: { categories: CategoryNode[] }) {
  return (
    <Suspense fallback={<CategoryChips categories={categories} />}>
      <CurrentCategoryChips categories={categories} />
    </Suspense>
  );
}

/**
 * HO-SHP-002 §11.1 `ShopHeader`, restyled as the Hearth header (ADR-FE-173) - cat-disc logo, a
 * sunken pill `SearchBox`, the account links, the warm Cart pill, and the category pill chips (a
 * plain `<details>` disclosure on small screens, so it works without JavaScript). The
 * `useSearchParams` islands are suspended one by one, each with a server-rendered fallback, so the
 * header, nav and search are in the HTML itself (S4) - never streamed behind a hidden boundary.
 */
export function ShopHeader({ categories, accountSlot, languageSlot }: ShopHeaderProps) {
  return (
    // Sticky (owner, 2026-10-18): the bar and the category pills stay at the top while scrolling.
    <header className="sticky top-0 z-40 border-b border-border bg-surface shadow-[0_8px_20px_rgba(31,29,26,0.06)]">
      {/* Tighter on a phone so the pinned header stays short: logo, Sign in and Cart on one row. */}
      <div className="mx-auto flex max-w-[1224px] flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2.5 md:gap-x-6 md:gap-y-3 md:px-8 md:py-4">
        <HearthLogo variant="shop" className="text-xl md:text-2xl" />
        <div className="order-3 w-full md:order-none md:w-auto md:flex-1">
          <Suspense fallback={<SearchForm />}>
            <SearchBox />
          </Suspense>
        </div>
        <div className="ml-auto flex items-center gap-4">
          <Suspense fallback={null}>{languageSlot}</Suspense>
          <Suspense
            fallback={
              <Link
                href="/sign-in"
                className="inline-flex min-h-11 items-center text-sm font-semibold text-accent-ink no-underline"
              >
                Sign in
              </Link>
            }
          >
            {accountSlot}
          </Suspense>
          {/* Issues.md #4 - the cart link and item count on every shop page (HO-ORD-002 §3.3 CartBadge). */}
          <CartBadge />
        </div>
      </div>
      <nav aria-label="Shop categories">
        <div className="mx-auto max-w-[1224px] px-4 md:px-8">
          <details className="group md:hidden">
            <summary className="flex min-h-10 cursor-pointer list-none items-center gap-2 pb-1 text-sm font-semibold text-ink">
              <Menu size={16} aria-hidden="true" />
              Browse the shop
            </summary>
            <div className="flex flex-wrap gap-2.5 pb-4">
              <CategoryLinks categories={categories} />
            </div>
          </details>
          <div className="hidden flex-wrap gap-2.5 pb-3.5 md:flex">
            <CategoryLinks categories={categories} />
          </div>
        </div>
      </nav>
    </header>
  );
}
