import Link from 'next/link';
import { Globe } from 'lucide-react';
import { countryFlag } from '@/lib/format/shop';
import { plainExcerpt } from '@/lib/shop/helpers';
import { cn } from '@/lib/utils';
import type { Origin } from '@/lib/api/shop-catalogue';

/** The flag is decorative (`alt=""`); the country name next to it is always text. */
export function FlagOrGlobe({ code, size = 28 }: { code: string; size?: number }) {
  const flag = countryFlag(code);
  return flag ? (
    // eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035: in-repo SVG flag
    <img
      src={flag}
      alt=""
      width={size}
      height={Math.round((size * 2) / 3)}
      className="flex-none rounded-[3px] shadow-[0_0_0_1px_rgba(0,0,0,0.14)]"
    />
  ) : (
    <Globe size={size - 4} aria-hidden="true" className="flex-none text-ink-muted" />
  );
}

/**
 * HO-SHP-002 §11.4 `OriginGrid` — US-SHP-05 AC1: a card per origin with its flag, country name
 * (`<h2>`), a PLAIN-TEXT excerpt of the sourcing notes (first 160 characters) and the product count.
 * Featured origins first, as the API orders them. `compact` is the storefront home's short card
 * (flag, name, count) in four columns.
 */
export function OriginGrid({
  origins,
  lang,
  headingLevel = 'h2',
  compact,
}: {
  origins: Origin[];
  lang?: string;
  headingLevel?: 'h2' | 'h3';
  compact?: boolean;
}) {
  const Heading = headingLevel;
  return (
    <ul
      className={cn(
        'm-0 grid list-none gap-4 p-0',
        compact
          ? 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-4'
          : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
      )}
    >
      {origins.map((origin) => {
        const excerpt = plainExcerpt(origin.sourcingNotes, 160);
        const count = `${origin.productCount} ${origin.productCount === 1 ? 'product' : 'products'}`;
        const href = `/shop/countries/${origin.code}${lang ? `?lang=${encodeURIComponent(lang)}` : ''}`;
        return (
          <li key={origin.code}>
            <article
              className={cn(
                'relative flex h-full rounded-card border border-border bg-surface p-5 hover:border-accent',
                compact ? 'items-center gap-3.5' : 'flex-col gap-2.5',
              )}
            >
              <div className="flex items-center gap-3.5">
                <FlagOrGlobe code={origin.code} size={compact ? 30 : 36} />
                <div className="min-w-0">
                  <Heading className="m-0 text-base font-semibold text-ink">
                    {/* The whole card is the link target. */}
                    <Link
                      href={href}
                      className="text-ink no-underline after:absolute after:inset-0 after:content-[''] hover:underline"
                    >
                      {origin.name}
                    </Link>
                  </Heading>
                  {compact && <p className="m-0 text-[13px] text-ink-muted">{count}</p>}
                </div>
              </div>
              {!compact && (
                <>
                  <p className="m-0 flex-1 text-sm text-ink-muted">
                    {excerpt || 'No sourcing notes yet.'}
                  </p>
                  <p className="m-0 text-sm font-semibold text-ink">{count}</p>
                </>
              )}
            </article>
          </li>
        );
      })}
    </ul>
  );
}
