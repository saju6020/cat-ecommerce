import Link from 'next/link';
import { HearthEyebrow } from '@/components/hearth/HearthBand';
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

/** HO-SHP-002 §11.3 `ProductHeading` — brand eyebrow, name (`<h1>`), rating text and the origin link. */
export function ProductHeading({
  name,
  nameLang,
  brand,
  rating,
  country,
  lang,
}: ProductHeadingProps) {
  return (
    <header className="flex flex-col gap-2.5">
      <HearthEyebrow className="tracking-[0.08em]">{brand}</HearthEyebrow>
      <h1
        lang={nameLang}
        className="m-0 font-display text-[clamp(1.9rem,4vw,2.75rem)] font-semibold leading-[1.06] text-ink"
      >
        {name}
      </h1>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
        <a
          href="#reviews"
          className="inline-flex min-h-8 items-center text-ink-muted no-underline hover:underline"
        >
          <RatingText rating={rating} />
        </a>
        {country && (
          <Link
            href={`/shop/countries/${country.code}${lang ? `?lang=${encodeURIComponent(lang)}` : ''}`}
            className="inline-flex min-h-8 items-center no-underline hover:underline"
          >
            <OriginText country={country} />
          </Link>
        )}
      </div>
    </header>
  );
}
