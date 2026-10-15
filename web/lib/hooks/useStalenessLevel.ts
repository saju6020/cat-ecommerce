'use client';

import { useEffect, useState } from 'react';

/**
 * `lib/hooks/useStalenessLevel.ts` — HO-XCT-002 §8.6. How old a saved copy is, in three steps.
 * It re-renders ONLY when the age crosses 15 or 60 minutes (a timeout to the next threshold, never
 * an interval), so the live region that shows it is announced at thresholds and never per tick.
 */
export type StalenessLevel = 'fresh' | 'over15' | 'over60';

export const STALE_15_MS = 15 * 60_000;
export const STALE_60_MS = 60 * 60_000;

export function stalenessOf(ageMs: number): StalenessLevel {
  if (ageMs >= STALE_60_MS) return 'over60';
  if (ageMs >= STALE_15_MS) return 'over15';
  return 'fresh';
}

export function useStalenessLevel(sinceMs: number | null): StalenessLevel {
  const [, setNow] = useState(0);
  const level = sinceMs === null ? 'fresh' : stalenessOf(Date.now() - sinceMs);

  useEffect(() => {
    if (sinceMs === null || level === 'over60') return;
    const threshold = level === 'fresh' ? STALE_15_MS : STALE_60_MS;
    const timeout = setTimeout(() => setNow((n) => n + 1), Math.max(0, sinceMs + threshold - Date.now()));
    return () => clearTimeout(timeout);
  }, [sinceMs, level]);

  return level;
}
