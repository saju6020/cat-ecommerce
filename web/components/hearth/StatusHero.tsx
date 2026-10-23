import * as React from 'react';
import { cn } from '@/lib/utils';
import { HearthBand, HearthEyebrow } from './HearthBand';
import { LineArtCat } from './LineArtCat';

/**
 * The status hero of the order placed / order detail screens (docs/design/orders/OrderPlaced.dc.html
 * `.hero`): a warm-soft band with an eyebrow, a large display-font headline, a supporting sentence,
 * actions, and a white disc with the line-art cat. Warmth stops at money and problems, so callers
 * use it for the happy path only.
 */
export function StatusHero({
  eyebrow,
  headline,
  headingId,
  children,
  actions,
  className,
}: {
  eyebrow: React.ReactNode;
  headline: React.ReactNode;
  headingId: string;
  /** The supporting sentence(s). */
  children?: React.ReactNode;
  actions?: React.ReactNode;
  className?: string;
}) {
  return (
    <HearthBand
      aria-labelledby={headingId}
      className={cn(
        'grid items-center gap-6 md:grid-cols-[minmax(0,1fr)_auto] md:px-10 md:py-9',
        className,
      )}
    >
      <div className="flex min-w-0 flex-col gap-2.5">
        <HearthEyebrow className="[overflow-wrap:anywhere]">{eyebrow}</HearthEyebrow>
        <h1
          id={headingId}
          className="m-0 font-display text-4xl font-semibold leading-[1.05] text-ink md:text-[44px]"
        >
          {headline}
        </h1>
        {children && <div className="text-base">{children}</div>}
        {actions && <div className="mt-1.5 flex flex-wrap gap-2.5">{actions}</div>}
      </div>
      <div
        aria-hidden="true"
        className="hidden size-[200px] place-items-center rounded-pill bg-surface text-warm-ink md:grid"
      >
        <LineArtCat width={112} height={123} />
      </div>
    </HearthBand>
  );
}
