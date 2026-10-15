'use client';

import { useCallback, useRef, useState } from 'react';

/**
 * `lib/hooks/useScanner.ts` — HO-ORD-002 ADR-FE-095, HW-2. A barcode scanner in keyboard-wedge mode is
 * a very fast keyboard that ends with Enter. Characters that each arrive less than `SCAN_GAP_MS` after
 * the previous one and finish with Enter are a **scan**; slower typing is **manual entry**. Both are
 * submitted on Enter (so a person can type a barcode and press Enter without leaving the field);
 * neither fires a lookup on its own. No scanner library, no camera. The buffer is the input's own
 * text — it stays visible when a lookup fails.
 */
export const SCAN_GAP_MS = 50;
export const SCAN_MIN_LENGTH = 4;

export type ScanKind = 'scan' | 'manual';

export function useScanner(onSubmit: (value: string, kind: ScanKind) => void) {
  const [value, setValue] = useState('');
  const lastKeyAt = useRef(0);
  const burst = useRef(true);
  const length = useRef(0);

  const onKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLInputElement>) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        const text = event.currentTarget.value.trim();
        if (!text) return;
        const kind: ScanKind =
          burst.current && length.current >= SCAN_MIN_LENGTH && text.length >= SCAN_MIN_LENGTH
            ? 'scan'
            : 'manual';
        burst.current = true;
        length.current = 0;
        onSubmit(text, kind);
        return;
      }
      if (event.key.length !== 1 || event.ctrlKey || event.metaKey || event.altKey) return;
      const now = event.timeStamp;
      if (length.current > 0 && now - lastKeyAt.current >= SCAN_GAP_MS) burst.current = false;
      lastKeyAt.current = now;
      length.current += 1;
    },
    [onSubmit],
  );

  const clear = useCallback(() => {
    setValue('');
    burst.current = true;
    length.current = 0;
  }, []);

  return { value, setValue, onKeyDown, clear };
}
