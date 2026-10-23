'use client';

import { useSearchParams } from 'next/navigation';
import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';

/**
 * HO-SHP-002 §11.1 `SearchBox` — `role="search"`, labelled *Search products*; submits `q` to
 * `/shop/products` and keeps `lang`. A plain GET form, so it works before hydration. No
 * suggestions (OQ-4). Keyed on the current `q` so a new search shows its own text.
 */
export function SearchBox() {
  const searchParams = useSearchParams();
  return <SearchForm q={searchParams.get('q') ?? ''} lang={searchParams.get('lang') ?? ''} />;
}

/**
 * The form itself, with no dependency on the URL. `ShopHeader` uses it as the `Suspense` fallback
 * of `SearchBox` (S4), so a statically rendered page still ships a working search form in its HTML
 * without JavaScript. Hearth: a 46px sunken pill with a teal pill Search button (ADR-FE-173).
 */
export function SearchForm({ q = '', lang = '' }: { q?: string; lang?: string }) {
  return (
    <form
      role="search"
      aria-label="Products"
      action="/shop/products"
      method="get"
      className="flex min-h-[46px] w-full items-center gap-2 rounded-pill bg-surface-sunken py-0.5 pl-[18px] pr-[5px] text-ink-muted md:max-w-xl"
    >
      <Search size={18} aria-hidden="true" className="flex-none" />
      <label htmlFor="shop-search" className="sr-only">
        Search products
      </label>
      <input
        key={q}
        id="shop-search"
        type="search"
        name="q"
        defaultValue={q}
        maxLength={100}
        placeholder="Search food, litter, brands, ingredients"
        className="h-11 min-w-0 flex-1 bg-transparent text-[15px] text-ink placeholder:text-ink-muted focus-visible:outline-offset-4"
      />
      {lang && <input type="hidden" name="lang" value={lang} />}
      <Button
        type="submit"
        variant="pill"
        size="pillMd"
        className="min-h-9 px-4"
        aria-label="Search"
      >
        Search
      </Button>
    </form>
  );
}
