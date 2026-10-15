'use client';

import { useCallback, useRef } from 'react';

/**
 * `lib/hooks/billing/useIdempotencyKey.ts` — HO-BIL-002 §8.3, ADR-FE-081. One `crypto.randomUUID()`
 * per form mount, kept across every retry of the same submit (a network error, a `5xx`, a double
 * click), and renewed only after a success or an explicit "start again". The server stores
 * `(caller, key)`; a repeat answers `200` with the original body, so a lost response followed by a
 * retry never records twice. The ref (not state) keeps `current()` correct inside an async submit.
 */
export function useIdempotencyKey(): { current: () => string; renew: () => void } {
  const keyRef = useRef<string | null>(null);

  const current = useCallback(() => {
    if (keyRef.current === null) keyRef.current = crypto.randomUUID();
    return keyRef.current;
  }, []);

  const renew = useCallback(() => {
    keyRef.current = crypto.randomUUID();
  }, []);

  return { current, renew };
}
