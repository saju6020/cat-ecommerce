import * as React from 'react';
import { cn } from '@/lib/utils';

export interface HearthBandProps extends React.HTMLAttributes<HTMLElement> {
  /** The element to render; a `section` by default. */
  as?: 'section' | 'div' | 'aside' | 'header';
}

/**
 * Warm-soft rounded band for heroes, greetings, announcements and empty states. Warmth stops at
 * medical and money: never wrap a PrescriptionGate, maintenance notice or error in one.
 */
export function HearthBand({ as: Tag = 'section', className, ...props }: HearthBandProps) {
  return <Tag className={cn('rounded-band bg-warm-soft p-8 md:p-12', className)} {...props} />;
}

/** The 12px uppercase eyebrow used inside bands, in warm-ink. */
export function HearthEyebrow({ className, ...props }: React.HTMLAttributes<HTMLParagraphElement>) {
  return (
    <p
      className={cn(
        'm-0 text-xs font-semibold uppercase tracking-[0.07em] text-warm-ink',
        className,
      )}
      {...props}
    />
  );
}
