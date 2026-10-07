import Link from 'next/link';
import { Globe } from 'lucide-react';
import { countryFlag } from '@/lib/format/shop';
import { plainExcerpt } from '@/lib/shop/helpers';
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
      className="rounded-[3px]"
    />
  ) : (
    <Globe size={size - 4} aria-hidden="true" className="text-ink-muted" />
  );
}

/**
 * HO-SHP-002 §11.4 `OriginGrid` — US-SHP-05 AC1: a card per origin with its flag, country name
 * (`<h2>`), a PLAIN-TEXT excerpt of the sourcing notes (first 160 characters) and the product count.
 * Featured origins first, as the API orders them.
 */
export function OriginGrid({
  origins,
  lang,
  headingLevel = 'h2',
}: {
  origins: Origin[];
  lang?: string;
  headingLevel?: 'h2' | 'h3';
}) {
  const Heading = headingLevel;
  return (
    <ul className="grid list-none grid-cols-1 gap-3 p-0 sm:grid-cols-2 lg:grid-cols-3">
      {origins.map((origin) => {
        const excerpt = plainExcerpt(origin.sourcingNotes, 160);
        return (
          <li key={origin.code}>
            <article className="flex h-full flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
              <div className="flex items-center gap-3">
                <FlagOrGlobe code={origin.code} />
                <Heading className="text-lg font-semibold text-ink">
                  <Link
                    href={`/shop/countries/${origin.code}${lang ? `?lang=${encodeURIComponent(lang)}` : ''}`}
                    className="text-ink no-underline hover:underline"
                  >
                    {origin.name}
                  </Link>
                </Heading>
              </div>
              <p className="flex-1 text-sm text-ink-muted">{excerpt || 'No sourcing notes yet.'}</p>
              <p className="text-sm font-semibold text-ink">
                {origin.productCount} {origin.productCount === 1 ? 'product' : 'products'}
              </p>
            </article>
          </li>
        );
      })}
    </ul>
  );
}
