'use client';

import { useEffect, useRef, useState } from 'react';

/** The most often a screen reader is told the dashboard refreshed (ADR-FE-135). */
export const ANNOUNCE_AT_MOST_EVERY_MS = 60_000;

/**
 * `components/dashboard/RefreshAnnouncer.tsx` — HO-RPT-002 §11.2, ADR-FE-135, UI-5. A polite live
 * region that says "Dashboard updated" when a refetch brings new data, at most once a minute and
 * never for the first load. It never moves focus and never interrupts (`aria-live="polite"`).
 */
export function RefreshAnnouncer({ updatedAt }: { updatedAt: number }) {
  const [message, setMessage] = useState('');
  const lastUpdate = useRef(updatedAt);
  const lastAnnounced = useRef(0);

  useEffect(() => {
    if (updatedAt === lastUpdate.current) return;
    lastUpdate.current = updatedAt;
    const now = Date.now();
    if (now - lastAnnounced.current < ANNOUNCE_AT_MOST_EVERY_MS) return;
    lastAnnounced.current = now;
    setMessage('Dashboard updated');
    const clear = setTimeout(() => setMessage(''), 4_000);
    return () => clearTimeout(clear);
  }, [updatedAt]);

  return (
    <p role="status" aria-live="polite" className="sr-only">
      {message}
    </p>
  );
}
