'use client';

import { useEffect, useState } from 'react';

/**
 * `lib/hooks/useIdleTimeout.ts` — HO-XCT-002 §11.4, ADR-FE-144. True once `minutes` have passed with
 * no keyboard, pointer or touch input (counting idle time that passed before the page opened, and
 * calling `onIdle` so the lock can be persisted). Once idle it stays idle: the offline board cannot be
 * unlocked while offline, because signing in needs the server. Input before the deadline resets the
 * timer.
 */
const ACTIVITY_EVENTS = ['keydown', 'pointerdown', 'pointermove', 'touchstart', 'wheel'] as const;

export function useIdleTimeout(
  minutes: number,
  enabled: boolean = true,
  alreadyIdleMs: number = 0,
  onIdle?: () => void,
): boolean {
  const [idle, setIdle] = useState(false);

  useEffect(() => {
    if (!enabled || idle) return;
    const limitMs = Math.max(1, minutes) * 60_000;
    const fire = () => {
      setIdle(true);
      onIdle?.();
    };
    // The first deadline counts idle time already used (the device was idle before this page opened).
    let timer = setTimeout(fire, Math.max(0, limitMs - alreadyIdleMs));
    const reset = () => {
      clearTimeout(timer);
      timer = setTimeout(fire, limitMs);
    };
    for (const name of ACTIVITY_EVENTS) window.addEventListener(name, reset, { passive: true });
    return () => {
      clearTimeout(timer);
      for (const name of ACTIVITY_EVENTS) window.removeEventListener(name, reset);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [minutes, enabled, idle]);

  return idle;
}
