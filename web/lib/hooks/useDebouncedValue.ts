import { useEffect, useState } from 'react';

/** Returns `value` once it has stopped changing for `delayMs`. Used by the template preview
 *  (ADR-FE-163: 600 ms). The first value is returned immediately. */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}
