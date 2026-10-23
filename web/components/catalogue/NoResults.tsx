import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import { EmptyCatIllustration } from '@/components/common/EmptyState';
import { buttonVariants } from '@/components/ui/button';
import { HearthBand } from '@/components/hearth/HearthBand';
import { LineArtCat } from '@/components/hearth/LineArtCat';
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
          <Link
            href={clearHref}
            className={buttonVariants({ variant: 'pillSecondary', size: 'pillMd' })}
          >
            Clear filters
          </Link>
        }
      />
    );
  }
  const didYouMean = suggestions?.didYouMean ?? [];
  const popular = suggestions?.popular ?? [];
  return (
    <div className="flex flex-col gap-8">
      <HearthBand
        aria-labelledby="no-results-heading"
        className="flex flex-col items-center gap-5 text-center md:p-10"
      >
        <span
          aria-hidden="true"
          className="grid size-[120px] place-items-center rounded-pill bg-surface text-warm-ink"
        >
          <LineArtCat width={72} height={80} />
        </span>
        <div role="status" aria-live="polite" className="flex flex-col gap-2">
          <h2 id="no-results-heading" className="m-0 font-display text-2xl font-semibold text-ink">
            No products match “{q}”.
          </h2>
          <p className="m-0 text-sm text-ink-muted">
            {didYouMean.length > 0 ? (
              <>
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
              </>
            ) : (
              'Check the spelling, or try a more general word such as the brand or the food type.'
            )}
          </p>
        </div>
      </HearthBand>
      {popular.length > 0 && (
        <section aria-labelledby="popular-heading" className="flex flex-col gap-3">
          <h2
            id="popular-heading"
            className="m-0 font-display text-[clamp(1.5rem,3vw,2rem)] font-semibold text-ink"
          >
            Popular right now
          </h2>
          <ProductGrid items={popular} lang={lang} headingLevel="h3" />
        </section>
      )}
    </div>
  );
}
