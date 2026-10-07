'use client';

import { useEffect, useState } from 'react';
import { getServerClockOffsetMs } from '@/lib/api/client';

/**
 * `lib/hooks/useServerNow.ts` — HO-APT-002 §8.4, ADR-FE-025. The server's "now", in epoch ms:
 * `Date.now()` plus the median offset of the last five `Date` response headers (recorded by
 * `client.ts`). The hold countdown and every "can I still change this" hint read it, so a phone
 * with a wrong clock neither shortens nor lengthens a hold. Re-renders every `tickMs`.
 */
export function useServerNow(tickMs = 1000): number {
  const [now, setNow] = useState(() => Date.now() + getServerClockOffsetMs());

  useEffect(() => {
    const tick = () => setNow(Date.now() + getServerClockOffsetMs());
    tick();
    const id = setInterval(tick, tickMs);
    return () => clearInterval(id);
  }, [tickMs]);

  return now;
}
