'use client';

import { useEffect, useRef, useState } from 'react';
import { Hourglass } from 'lucide-react';
import { useServerNow } from '@/lib/hooks/useServerNow';
import { formatBranchInstant, formatTimeLeft } from '@/lib/format/boarding';

const HOUR = 3_600_000;

type Threshold = 'none' | 'day' | 'hour' | 'expired';

const ANNOUNCEMENT: Record<Threshold, string> = {
  none: '',
  day: 'Less than 24 hours left to pay the deposit.',
  hour: 'Less than 1 hour left to pay the deposit.',
  expired: 'The time to pay the deposit has ended. The reservation may be released.',
};

function thresholdFor(remainingMs: number): Threshold {
  if (remainingMs <= 0) return 'expired';
  if (remainingMs <= HOUR) return 'hour';
  if (remainingMs <= 24 * HOUR) return 'day';
  return 'none';
}

/**
 * `components/boarding/shared/PayByCountdown.tsx` — HO-BRD-002 §11.1/§11.3, U-A10, UI-6. The time
 * left to pay a deposit, counted against the SERVER's clock (`useServerNow`, HO-APT-002 ADR-FE-025)
 * so a phone with the wrong time neither shortens nor lengthens it. The visible time ticks every
 * second but is NOT a live region; a separate polite one announces only when the remaining time
 * crosses 24 h, 1 h and expiry. This only tells the owner how long they have — it takes no payment
 * (ADR-FE-106).
 */
export function PayByCountdown({ payBy, zone }: { payBy: string; zone: string }) {
  const now = useServerNow(1000);
  const remaining = new Date(payBy).getTime() - now;
  const threshold = thresholdFor(remaining);
  const announced = useRef<Threshold>(threshold);
  const [announcement, setAnnouncement] = useState('');

  useEffect(() => {
    if (announced.current === threshold) return;
    announced.current = threshold;
    setAnnouncement(ANNOUNCEMENT[threshold]);
  }, [threshold]);

  return (
    <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-ink">
      <Hourglass size={16} className="flex-none text-warning-ink" aria-hidden="true" />
      <span>
        Pay by <time dateTime={payBy}>{formatBranchInstant(payBy, zone)}</time>{' '}
        <span className="text-ink-muted">({zone})</span>
      </span>
      <span
        data-testid="pay-by-left"
        className={
          threshold === 'expired' || threshold === 'hour'
            ? 'font-semibold text-danger-ink'
            : 'font-semibold text-ink'
        }
      >
        · {formatTimeLeft(remaining)}
      </span>
      <span role="status" aria-live="polite" className="sr-only">
        {announcement}
      </span>
    </p>
  );
}
