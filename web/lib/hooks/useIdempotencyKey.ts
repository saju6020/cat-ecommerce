'use client';

import { useCallback, useRef } from 'react';
import { shouldRenewIdempotencyKey } from '@/lib/api/errors';

/**
 * `lib/hooks/useIdempotencyKey.ts` — HO-ORD-002 §8.3, ADR-FE-097, US-ORD-18. One
 * `crypto.randomUUID()` per *attempt*: `keyFor(signature)` returns the SAME key for every retry of
 * the same payload (a double click, a manual retry after a lost response), a NEW key when the payload
 * (its `signature`) is different, and `renew()` forgets it after a success so the next order, sale or
 * refund starts fresh. The key lives in a ref — never in storage, a URL or a query cache — and
 * nothing here retries by itself.
 */
export function useIdempotencyKey(): {
  keyFor: (signature: string) => string;
  renew: () => void;
  /** After a failure: forget the key when the API gave a definitive refusal (see `shouldRenewIdempotencyKey`). */
  settleFailure: (error: unknown) => void;
} {
  const held = useRef<{ key: string; signature: string } | null>(null);

  const keyFor = useCallback((signature: string) => {
    if (held.current === null || held.current.signature !== signature) {
      held.current = { key: crypto.randomUUID(), signature };
    }
    return held.current.key;
  }, []);

  const renew = useCallback(() => {
    held.current = null;
  }, []);

  const settleFailure = useCallback((error: unknown) => {
    if (shouldRenewIdempotencyKey(error)) held.current = null;
  }, []);

  return { keyFor, renew, settleFailure };
}
