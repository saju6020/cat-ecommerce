import { useRef } from 'react';

/**
 * `lib/hooks/useFrozenWhile.ts` — HO-APT-002 ADR-FE-027. While `frozen` is true (a drag is in
 * progress) the hook keeps returning the value it had when the freeze began; the moment `frozen`
 * turns false it returns the latest value. A 30 s poll that lands mid-drag therefore updates the
 * cache but not what is on screen until the drop — "applied after the drag ends, not during".
 */
export function useFrozenWhile<T>(value: T, frozen: boolean): T {
  const held = useRef<{ value: T } | null>(null);
  if (!frozen) {
    held.current = null;
    return value;
  }
  if (held.current === null) held.current = { value };
  return held.current.value;
}
