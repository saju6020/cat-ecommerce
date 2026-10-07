import Link from 'next/link';
import { OriginText, RatingText } from './StatusText';
import type { CountryRef, RatingSummary } from '@/lib/api/shop-catalogue';

export interface ProductHeadingProps {
  name: string;
  /** Language the name was served in (`fieldLocales.name`) — WCAG 3.1.2. */
  nameLang: string;
  brand: string;
  rating: RatingSummary;
  country: CountryRef | null;
  lang?: string;
}

/** HO-SHP-002 §11.3 `ProductHeading` — name (`<h1>`), brand, rating text and the origin link. */
export function ProductHeading({
  name,
  nameLang,
  brand,
  rating,
  country,
  lang,
}: ProductHeadingProps) {
  return (
    <header className="flex flex-col gap-2">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{brand}</p>
      <h1 lang={nameLang} className="font-display text-2xl font-semibold text-ink md:text-3xl">
        {name}
      </h1>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
        <a href="#reviews" className="text-ink-muted no-underline hover:underline">
          <RatingText rating={rating} />
        </a>
        {country && (
          <Link
            href={`/shop/countries/${country.code}${lang ? `?lang=${encodeURIComponent(lang)}` : ''}`}
            className="no-underline hover:underline"
          >
            <OriginText country={country} />
          </Link>
        )}
      </div>
    </header>
  );
}
