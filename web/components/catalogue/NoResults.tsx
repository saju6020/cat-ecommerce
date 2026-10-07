import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import { EmptyCatIllustration } from '@/components/common/EmptyState';
import { buttonVariants } from '@/components/ui/button';
import { ProductGrid } from './ProductGrid';
import type { CatalogueItem } from '@/lib/api/shop-catalogue';

export interface NoResultsProps {
  q: string;
  suggestions: { didYouMean: string[]; popular: CatalogueItem[] } | null;
  /** `href` that rewrites `q` and keeps every filter, sort and language. */
  hrefForQuery: (q: string) => string;
  /** `href` that drops the filters and keeps `q`. */
  clearHref: string;
  lang?: string;
}

/**
 * HO-SHP-002 §11.2 `NoResults` — US-SHP-06 AC3. With a search: the sentence, *Did you mean* links
 * and the *Popular right now* grid, all from the server's `suggestions`. Without one (filters
 * only): *Nothing matches these filters* and *Clear filters*.
 */
export function NoResults({ q, suggestions, hrefForQuery, clearHref, lang }: NoResultsProps) {
  if (!q) {
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="Nothing matches these filters"
        description="Try removing a filter or two."
        action={
          <Link href={clearHref} className={buttonVariants({ variant: 'secondary' })}>
            Clear filters
          </Link>
        }
      />
    );
  }
  const didYouMean = suggestions?.didYouMean ?? [];
  const popular = suggestions?.popular ?? [];
  return (
    <div className="flex flex-col gap-6">
      <EmptyState
        icon={<EmptyCatIllustration />}
        title={`No products match “${q}”.`}
        description={
          didYouMean.length > 0 ? (
            <span>
              Did you mean{' '}
              {didYouMean.map((term, index) => (
                <span key={term}>
                  {index > 0 && ', '}
                  <Link href={hrefForQuery(term)} className="font-semibold text-accent-ink">
                    {term}
                  </Link>
                </span>
              ))}
              ?
            </span>
          ) : (
            'Check the spelling, or try a more general word such as the brand or the food type.'
          )
        }
      />
      {popular.length > 0 && (
        <section aria-labelledby="popular-heading" className="flex flex-col gap-3">
          <h2 id="popular-heading" className="font-display text-lg font-semibold text-ink">
            Popular right now
          </h2>
          <ProductGrid items={popular} lang={lang} headingLevel="h3" />
        </section>
      )}
    </div>
  );
}
