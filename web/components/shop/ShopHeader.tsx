import * as React from 'react';
import { Suspense } from 'react';
import Link from 'next/link';
import { Menu } from 'lucide-react';
import { SearchBox, SearchForm } from './SearchBox';
import { categoryLabel } from '@/lib/shop/labels';
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
    <>
      <Link href="/shop/products" className="text-ink no-underline hover:underline">
        All products
      </Link>
      <Link href="/shop/countries" className="text-ink no-underline hover:underline">
        Shop by country
      </Link>
      {categories.map((category) => (
        <Link
          key={category.value}
          href={`/shop/products?category=${encodeURIComponent(category.value)}`}
          className="text-ink no-underline hover:underline"
        >
          {categoryLabel(category.value)}
        </Link>
      ))}
    </>
  );
}

/**
 * HO-SHP-002 §11.1 `ShopHeader` — logo, `SearchBox`, the category navigation (a plain `<details>`
 * disclosure on small screens, so it works without JavaScript), and the account links. The
 * `useSearchParams` islands are suspended one by one, each with a server-rendered fallback, so the
 * header, nav and search are in the HTML itself (S4) - never streamed behind a hidden boundary.
 */
export function ShopHeader({ categories, accountSlot, languageSlot }: ShopHeaderProps) {
  return (
    <header className="border-b border-border bg-surface">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3 md:px-8">
        <Link
          href="/shop"
          className="font-display text-xl font-semibold text-accent-ink no-underline"
        >
          PurrCare Shop
        </Link>
        <div className="order-3 w-full md:order-none md:w-auto md:flex-1">
          <Suspense fallback={<SearchForm />}>
            <SearchBox />
          </Suspense>
        </div>
        <div className="ml-auto flex items-center gap-4">
          <Suspense fallback={null}>{languageSlot}</Suspense>
          <Suspense
            fallback={
              <Link href="/sign-in" className="text-sm font-semibold text-accent-ink no-underline">
                Sign in
              </Link>
            }
          >
            {accountSlot}
          </Suspense>
        </div>
      </div>
      <nav aria-label="Shop categories" className="border-t border-border">
        <div className="mx-auto max-w-6xl px-4 md:px-8">
          <details className="group md:hidden">
            <summary className="flex min-h-10 cursor-pointer list-none items-center gap-2 py-2 text-sm font-semibold text-ink">
              <Menu size={16} aria-hidden="true" />
              Browse the shop
            </summary>
            <div className="flex flex-col gap-3 pb-3 text-sm">
              <CategoryLinks categories={categories} />
            </div>
          </details>
          <div className="hidden flex-wrap gap-x-6 gap-y-2 py-2 text-sm font-semibold md:flex">
            <CategoryLinks categories={categories} />
          </div>
        </div>
      </nav>
    </header>
  );
}
