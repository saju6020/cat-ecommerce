import { Check, Globe, ShieldAlert, Star, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { countryFlag, formatRating } from '@/lib/format/shop';
import { availabilityText } from '@/lib/shop/labels';
import { cn } from '@/lib/utils';
import type { Availability, CountryRef, RatingSummary } from '@/lib/api/shop-catalogue';

/** Availability: text plus an icon, never colour alone (UI-6). */
export function AvailabilityText({ availability }: { availability: Availability }) {
  const inStock = availability === 'InStock';
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 text-[13px] font-medium',
        inStock ? 'text-accent-ink' : 'text-danger-ink',
      )}
    >
      {inStock ? (
        <Check size={14} strokeWidth={2.4} aria-hidden="true" />
      ) : (
        <X size={14} strokeWidth={2.4} aria-hidden="true" />
      )}
      {availabilityText(availability)}
    </span>
  );
}

/** "4.5 out of 5 (23 reviews)" as text; the stars are decorative (`aria-hidden`). */
export function RatingText({ rating }: { rating: RatingSummary }) {
  const average = rating.count > 0 ? (rating.average ?? 0) : 0;
  return (
    <span className="inline-flex items-center gap-1.5 text-[13px] text-ink-muted">
      <span aria-hidden="true" className="inline-flex text-warm-ink">
        {Array.from({ length: 5 }, (_, i) => (
          <Star
            key={i}
            size={13}
            fill={i < Math.round(average) ? 'currentColor' : 'none'}
            strokeWidth={1.5}
          />
        ))}
      </span>
      <span>{formatRating(rating.average, rating.count)}</span>
    </span>
  );
}

/** Flag (decorative, `alt=""`) next to the country name, which is always shown as text. */
export function OriginText({ country }: { country: CountryRef }) {
  const flag = countryFlag(country.code);
  return (
    <span className="inline-flex items-center gap-1.5 text-sm text-ink-muted">
      {flag ? (
        // eslint-disable-next-line @next/next/no-img-element -- ADR-FE-035: in-repo SVG flag
        <img src={flag} alt="" width={18} height={12} className="h-3 w-[18px] rounded-[2px]" />
      ) : (
        <Globe size={14} aria-hidden="true" />
      )}
      {country.name}
    </span>
  );
}

export function PrescriptionBadge() {
  return (
    <Badge
      variant="warning"
      className="max-w-full whitespace-normal"
      icon={<ShieldAlert size={12} aria-hidden="true" />}
    >
      Vet authorisation needed
    </Badge>
  );
}
