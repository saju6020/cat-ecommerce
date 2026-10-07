'use client';

import { useEffect, useRef, useState } from 'react';
import { AlarmClock, Timer } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatCountdown } from '@/lib/format/appointment';
import { useServerNow } from '@/lib/hooks/useServerNow';

/** Seconds remaining at which a screen-reader announcement is made (ADR-FE-025). */
export const ANNOUNCE_AT_SECONDS = [300, 60, 0] as const;

const ANNOUNCEMENTS: Record<number, string> = {
  300: 'You have 5 minutes left to confirm your booking.',
  60: 'You have 1 minute left to confirm your booking.',
  0: 'Your hold has ended — please choose a time again.',
};

/**
 * ⭐ `components/booking/HoldCountdown.tsx` — HO-APT-002 §11.1, ADR-FE-025, US-CLN-03 AC1/AC2.
 * Counts down to `holdExpiresOn` on the SERVER's clock (`useServerNow`: browser time plus the
 * median offset of the last five `Date` headers), so a phone with a wrong clock neither shortens
 * nor lengthens the hold. The visible `mm:ss` is a `role="timer"` (not announced every second);
 * a separate polite live region announces only at 5:00, 1:00 and 0:00 — thresholds already behind
 * the starting time are skipped, never announced late. At 0 it calls `onExpire` exactly once.
 * Under one minute the timer turns amber **with an icon**, never colour alone (UI-6).
 *
 * `label` names whose hold it is (a booking or a waitlist offer) for the timer's accessible name.
 */
export function HoldCountdown({
  holdExpiresOn,
  onExpire,
  label = 'Time left to confirm',
}: {
  holdExpiresOn: string;
  onExpire: () => void;
  label?: string;
}) {
  const now = useServerNow();
  const expiresMs = new Date(holdExpiresOn).getTime();
  const remainingMs = Math.max(0, expiresMs - now);
  const remainingSeconds = Math.ceil(remainingMs / 1000);

  const announced = useRef<Set<number> | null>(null);
  if (announced.current === null) {
    // Thresholds the hold has already passed when it first appears are never announced.
    announced.current = new Set(ANNOUNCE_AT_SECONDS.filter((t) => remainingSeconds < t));
  }
  const [announcement, setAnnouncement] = useState('');
  const expiredOnce = useRef(false);

  useEffect(() => {
    for (const threshold of ANNOUNCE_AT_SECONDS) {
      if (remainingSeconds <= threshold && !announced.current!.has(threshold)) {
        announced.current!.add(threshold);
        setAnnouncement(ANNOUNCEMENTS[threshold] ?? '');
      }
    }
    if (remainingSeconds <= 0 && !expiredOnce.current) {
      expiredOnce.current = true;
      onExpire();
    }
  }, [remainingSeconds, onExpire]);

  const urgent = remainingSeconds <= 60;
  const Icon = urgent ? AlarmClock : Timer;

  return (
    <div className="flex flex-col gap-1">
      <p className="text-sm font-semibold text-ink" id="hold-timer-label">
        {label}
      </p>
      <div
        role="timer"
        aria-labelledby="hold-timer-label"
        className={cn(
          'inline-flex w-fit items-center gap-2 rounded-[var(--radius-md)] px-4 py-2 font-display text-3xl font-semibold tabular-nums',
          urgent ? 'bg-warning-bg text-warning-ink' : 'bg-accent-soft text-accent-ink',
        )}
      >
        <Icon size={24} aria-hidden="true" />
        <span>{formatCountdown(remainingMs)}</span>
      </div>
      {urgent && remainingSeconds > 0 && (
        <p className="text-sm font-semibold text-warning-ink">Less than a minute left.</p>
      )}
      <p aria-live="polite" role="status" className="sr-only">
        {announcement}
      </p>
    </div>
  );
}
