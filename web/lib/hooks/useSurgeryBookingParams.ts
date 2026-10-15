'use client';

import { useCallback, useRef } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

/** The booking choices that live in the URL (HO-SRG-002 §8.3): reload-safe, and the calendar's
 *  empty-theatre-cell click can deep-link here. */
export const BOOKING_PARAMS = [
  'customerId',
  'catId',
  'serviceId',
  'date',
  'surgeonId',
  'assistantId',
  'theatreId',
] as const;

export type BookingParam = (typeof BOOKING_PARAMS)[number];
export type BookingChoices = Record<BookingParam, string>;

/**
 * Reads the booking choices from the URL and writes any change back with `router.replace` (a choice is
 * not a navigation: Back leaves the form instead of stepping through every select).
 *
 * Quick successive changes (two selects changed back to back) must merge, not overwrite each other. The
 * URL a `router.replace` produces lags the call, so every change is merged into `latest` SYNCHRONOUSLY and
 * remembered in `inFlight`; when the URL is later seen to equal one of those writes it is that write
 * landing (an intermediate one included) and `latest` is left alone. Only a URL that matches none of them
 * (Back, Forward, a link) is external and resets `latest` — the same rule as `useFilterUrlUpdate`.
 */
export function useSurgeryBookingParams(): {
  choices: BookingChoices;
  set: (changes: Partial<BookingChoices>) => void;
} {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const current = searchParams.toString();
  const latest = useRef<URLSearchParams | null>(null);
  const inFlight = useRef<string[]>([]);
  const seen = useRef<string | null>(null);

  if (seen.current !== current) {
    seen.current = current;
    const landed = inFlight.current.indexOf(current);
    if (landed >= 0) {
      inFlight.current = inFlight.current.slice(landed + 1);
    } else {
      inFlight.current = [];
      latest.current = null; // external navigation: start again from the URL
    }
  }

  const choices = Object.fromEntries(
    BOOKING_PARAMS.map((name) => [name, searchParams.get(name) ?? '']),
  ) as BookingChoices;

  const set = useCallback(
    (changes: Partial<BookingChoices>) => {
      const base = latest.current ?? new URLSearchParams(current);
      const next = new URLSearchParams(base);
      for (const [name, value] of Object.entries(changes)) {
        if (value) next.set(name, value);
        else next.delete(name);
      }
      const query = next.toString();
      if (query === base.toString()) return; // nothing changed: nothing to write
      latest.current = next;
      inFlight.current.push(query);
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [current, pathname, router],
  );

  return { choices, set };
}
